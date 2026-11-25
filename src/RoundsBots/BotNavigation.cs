using System.Linq;
using RoundsBots.Logic;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Рух бота по платформах: тримати дистанцію, зіскакувати вниз, коли там є земля,
    /// шукати край платформи, щоб спуститися до цілі, перестрибувати прірви, лізти по стінах.
    /// </summary>
    internal sealed class BotNavigation
    {
        private const float FlatGroundCheck = 3f;       // земля «одразу під ногами»
        private const float SafeDropDepth = 45f;         // нижче цієї глибини — лише межа арени
        private const float LookAhead = 1.5f;
        private const float DetourHeight = 3f;           // ціль нижча за нас — шукаємо спуск
        private const float ClimbHeight = 2.5f;          // ціль вища — стрибаємо/лізем
        private const int EdgeSearchSteps = 30;
        private const float StuckSeconds = 1f;
        private const float StuckSpeed = 0.5f;
        private const float MaxStuckElapsed = 1f;
        private const float AirJumpAfter = 0.35f;
        private const float RouteExtraDistance = 4f;
        private const float MaxCommitSeconds = 1.5f;
        private const float MaxClimbSeconds = 3f;
        private const float MaxBounceSeconds = 4.5f;
        private const float BouncedBelowY = -16f;   // нижче — вважаємо, що вже біля межі й відскочили/відскакуємо
        private int bounceColumn = -1;
        private bool voidFallLogged;
        private float voidSampleY;
        private float voidSampleAt;
        private bool bounced;
        private const float RepeatedStuckWindow = 3f;
        private const float ReverseSeconds = 2.5f;
        private const float SameSpotWindow = 10f;
        private const float SameSpotDistance = 1.5f;
        private Vector2 lastStuckPosition = new Vector2(float.MaxValue, float.MaxValue);
        private int sameSpotCount;
        private const float FlipHoldSeconds = 0.35f;  // аналіз: бот розвертався 82 рази/хв, людина 9
        private const float EmergencyFallY = -14f;    // нижче — ось-ось межа арени
        private const float StandingOffset = 0.5f;   // центр гравця вище центру клітинки
        private const float WallProbeDistance = 1.2f;
        private const float HazardEscapeMargin = 1f;
        private const float HazardLookAhead = 2.5f;

        /// <summary>Дальність стрибка з запису гравця: одинарний ~11–13, подвійний ~16.</summary>
        private static readonly float[] GapLandingDistances = { 4f, 7f, 10f, 13f, 16f, 20f };

        private static readonly (int x, int y)[] StartCellOffsets = { (0, 0), (0, -1), (-1, 0), (1, 0), (-1, -1), (1, -1) };

        private readonly CharacterData data;
        private float stuckTimer;
        private float lastX;
        private float lastThinkTime;
        private int lastHorizontal = 1;
        private NavStep? plannedStep;
        private float plannedAt;
        private bool airJumpUsed;
        private bool airborneNow;
        private float lastStuckAt = float.NegativeInfinity;
        private float reverseUntil;
        private int reverseDirection;
        private float lastAirY;
        private static readonly HarmonyLib.AccessTools.FieldRef<HealthHandler, float> LastDamaged =
            HarmonyLib.AccessTools.FieldRefAccess<HealthHandler, float>("lastDamaged");

        private const float AttemptTimeout = 2.5f;
        private const float FellBelow = 2.5f;

        /// <summary>Поточна спроба кроку маршруту: щоб помітити, що він не вдається, і запам'ятати це для карти.</summary>
        private (int fx, int fy, int tx, int ty, NavMove move, float at, float lowY, int generation)? attempt;
        private float airborneSince = float.NegativeInfinity;
        private static readonly HarmonyLib.AccessTools.FieldRef<PlayerVelocity, Vector2> Velocity =
            HarmonyLib.AccessTools.FieldRefAccess<PlayerVelocity, Vector2>("velocity");
        private int lastOutput;
        private float lastFlipAt = float.NegativeInfinity;

        public BotNavigation(CharacterData data)
        {
            this.data = data;
            Reset();
        }

        public void Reset()
        {
            lastX = data.transform.position.x;
            lastThinkTime = Time.time;
            stuckTimer = 0f;
            attempt = null;       // смерть, пауза, вибір карток — незавершена спроба не рахується невдачею
            plannedStep = null;
            BreakTarget = null;
            gapJumpDirection = 0;
        }

        /// <summary>Повертає горизонтальний напрямок (-1/0/1) і чи треба стрибнути.</summary>
        public (int horizontal, bool jump) Decide(Vector2 myPosition, Vector2 toTarget, bool canSee, BotSettings settings)
        {
            ForgetSpentBounce(myPosition);
            (int horizontal, bool jump) = DecideRaw(myPosition, toTarget, canSee, settings);
            int voidEscape = VoidEscapeDirection(myPosition);
            if (voidEscape != 0)
            {
                // Над порожнечею — до найближчої опори, без згладжування (кожна мить у повітрі — ризик удару об межу).
                lastOutput = voidEscape;
                TrackVoidFall(myPosition, canSee, toTarget, voidEscape);
                return (voidEscape, jump || EmergencyJump(myPosition));
            }

            int smoothed = Smooth(horizontal);
            TrackVoidFall(myPosition, canSee, toTarget, smoothed);
            return (smoothed, jump || EmergencyJump(myPosition));
        }

        /// <summary>
        /// Замір (ШІ-чат, крок 1 про порожнечу, без зміни поведінки): бот падає над прірвою — один рядок VOID_FALL
        /// на падіння: кулдаун блоку, запланований рух, стрибки, швидкість, рішення по горизонталі.
        /// </summary>
        private void TrackVoidFall(Vector2 myPosition, bool canSee, Vector2 toTarget, int horizontal)
        {
            float now = Time.time;
            float vy = now > voidSampleAt ? (myPosition.y - voidSampleY) / (now - voidSampleAt) : 0f;
            voidSampleY = myPosition.y;
            voidSampleAt = now;
            if (data.isGrounded || data.isWallGrab)
            {
                voidFallLogged = false;
                return;
            }

            if (voidFallLogged || vy > 0f || data.ThereIsGroundBelow(myPosition, SafeDropDepth))
            {
                return;
            }

            voidFallLogged = true;
            string planned = plannedStep.HasValue ? plannedStep.Value.Move.ToString() : "-";
            bool blockCd = data.block == null || data.block.IsOnCD();
            BotLog.Write(System.FormattableString.Invariant(
                $"VOID_FALL bot={data.player.playerID} blockCD={blockCd} planned={planned} jumps={data.currentJumps} vy={vy:F1} move={horizontal} canSee={canSee} dist={toTarget.magnitude:F1} pos=({myPosition.x:F1},{myPosition.y:F1})"));
        }

        /// <summary>Не розвертаємося туди-сюди частіше ніж раз на <see cref="FlipHoldSeconds"/>: між розворотами стоїмо.</summary>
        private int Smooth(int horizontal)
        {
            if (horizontal != 0 && lastOutput != 0 && horizontal != lastOutput)
            {
                if (Time.time - lastFlipAt < FlipHoldSeconds)
                {
                    return 0;
                }

                lastFlipAt = Time.time;
            }

            if (horizontal != 0)
            {
                lastOutput = horizontal;
            }

            return horizontal;
        }

        private float bounceApexY = float.NegativeInfinity;
        private const float BounceSpentDrop = 2f;

        /// <summary>
        /// Відскок уже стався, бот злетів і знову падає — «bounced» більше не рятує (лог: бот 0 упав за межу з planned=Bounce,
        /// блок на кулдауні). Скидаємо план: далі діє порятунок на опору.
        /// </summary>
        private void ForgetSpentBounce(Vector2 myPosition)
        {
            if (!plannedStep.HasValue || plannedStep.Value.Move != NavMove.Bounce || !bounced)
            {
                bounceApexY = float.NegativeInfinity;
                return;
            }

            bounceApexY = Mathf.Max(bounceApexY, myPosition.y);
            if (BotMath.BounceSpent(bounceApexY, myPosition.y, BouncedBelowY, BounceSpentDrop))
            {
                plannedStep = null;
                bounced = false;
                bounceApexY = float.NegativeInfinity;
            }
        }

        private const int VoidSearchRadius = 40;   // уся ширина арени (гра 2026-11-25, «рука»: вихід з тунелю над прірвою був за 25 од., бот шукав лише ±12 і загинув)

        /// <summary>Відскок від низу ще має сенс: уже відскочили, або блок готовий прийняти удар об межу.</summary>
        private bool BounceStillSafe =>
            plannedStep.HasValue && plannedStep.Value.Move == NavMove.Bounce
            && (bounced || (data.block != null && !data.block.IsOnCD()));

        /// <summary>
        /// Режим «порожнеча»: у повітрі, під нами до межі арени нічого немає, і запланований відскок не рятує
        /// (блок на кулдауні) — напрямок до найближчої опори (±<see cref="VoidSearchRadius"/>), інакше 0.
        /// </summary>
        private int VoidEscapeDirection(Vector2 myPosition)
        {
            if (data.isGrounded || data.isWallGrab || data.ThereIsGroundBelow(myPosition, SafeDropDepth) || BounceStillSafe)
            {
                return 0;
            }

            if (plannedStep.HasValue && plannedStep.Value.Move == NavMove.Bounce)
            {
                plannedStep = null; // блок витрачено — відскок коштуватиме 51 HP, рятуємося на опору
            }

            bool committedRouteJump = false;
            if (plannedStep.HasValue && (plannedStep.Value.Move == NavMove.Jump || plannedStep.Value.Move == NavMove.Fall))
            {
                // Вікно довіри — від відриву, а не від рішення стрибати (розбіг його з'їдав). Уже падаємо і попереду
                // по дорозі до цілі кроку опори немає — стрибок провалено, рятуємося.
                float since = Time.time - Mathf.Max(plannedAt, airborneSince);
                int toLanding = NavMap.ToWorld(plannedStep.Value.X, plannedStep.Value.Y).x > myPosition.x ? 1 : -1;
                bool rising = data.playerVel != null && Velocity(data.playerVel).y > 0f;
                committedRouteJump = since < CommitSeconds(plannedStep.Value.Move) && (rising || HasLandingAhead(myPosition, toLanding));
            }
            bool landingAhead = gapJumpDirection != 0 && HasLandingAhead(myPosition, gapJumpDirection);
            int? keepGoing = BotMath.GapJumpEscape(committedRouteJump, gapJumpDirection, Time.time - gapJumpAt, landingAhead);
            if (keepGoing.HasValue)
            {
                return keepGoing.Value;
            }

            for (int distance = 1; distance <= VoidSearchRadius; distance++)
            {
                bool right = data.ThereIsGroundBelow(myPosition + Vector2.right * distance, SafeDropDepth);
                bool left = data.ThereIsGroundBelow(myPosition + Vector2.left * distance, SafeDropDepth);
                if (right || left)
                {
                    return right && left ? lastOutput != 0 ? lastOutput : 1 : right ? 1 : -1;
                }
            }

            return 0;
        }

        /// <summary>Падаємо до межі арени — останній шанс: стрибок (якщо ще є) у бік найближчої опори.</summary>
        private bool EmergencyJump(Vector2 myPosition)
        {
            bool bouncing = BounceStillSafe;
            return !bouncing && !data.isGrounded && myPosition.y < EmergencyFallY && data.currentJumps > 0
                && data.sinceGrounded > AirJumpAfter;
        }

        private bool CanJumpInAir => data.isWallGrab || data.currentJumps > 0;

        private int gapJumpDirection;
        private float gapJumpAt = -10f;

        /// <summary>Узятий відскок, але ще не відскочили від низу — падаємо колоною.</summary>
        private bool FallingToBounce => plannedStep.HasValue && plannedStep.Value.Move == NavMove.Bounce && !bounced;

        private (int horizontal, bool jump) DecideRaw(Vector2 myPosition, Vector2 toTarget, bool canSee, BotSettings settings)
        {
            // Поруч пилка — насамперед тікаємо від неї.
            Vector2 escape = HazardCache.EscapeDirection(myPosition, HazardEscapeMargin);
            if (escape != Vector2.zero)
            {
                int away = Mathf.Abs(escape.x) > 0.1f ? (escape.x > 0f ? 1 : -1) : -lastHorizontal;
                lastHorizontal = away;
                return (away, escape.y > 0.3f);
            }

            // Застрягли кілька разів поспіль — коротко йдемо у протилежний бік, щоб вирватися.
            if (Time.time < reverseUntil)
            {
                return (reverseDirection, false);
            }

            // Ціль не видно або вона далеко — йдемо за маршрутом A* по всій карті.
            bool needRoute = !canSee || toTarget.magnitude > settings.PreferredDistance + RouteExtraDistance;
            if (needRoute && TryFollowPath(myPosition, toTarget, out int routeHorizontal, out bool routeJump))
            {
                if (routeHorizontal != 0)
                {
                    lastHorizontal = routeHorizontal;
                }

                if (data.isWallGrab && toTarget.y > ClimbHeight)
                {
                    routeJump = true;
                }

                bool stuck = UpdateStuck(routeHorizontal); // завжди оновлюємо лічильник (без short-circuit)
                if (FallingToBounce)
                {
                    return (routeHorizontal, false); // стрибок зі стіни зірвав би відскок — просто падаємо
                }

                return (routeHorizontal, routeJump || (stuck && !airborneNow));
            }

            int horizontal = ChooseDirection(myPosition, toTarget, canSee, settings);
            bool jump = false;

            // Не йдемо в пилку попереду.
            if (horizontal != 0 && HazardCache.IsDangerous(myPosition + new Vector2(horizontal * HazardLookAhead, 0f)))
            {
                horizontal = 0;
            }

            if (horizontal != 0 && IsEdgeAhead(myPosition, horizontal))
            {
                if (HasGroundFarBelow(myPosition, horizontal))
                {
                    // Нижче є платформа — просто зіскакуємо, не боїмося.
                }
                else if (HasLandingAhead(myPosition, horizontal))
                {
                    jump = true; // перестрибуємо прірву
                    gapJumpDirection = horizontal;
                    gapJumpAt = Time.time;
                }
                else
                {
                    horizontal = 0;
                }
            }

            if (horizontal != 0)
            {
                lastHorizontal = horizontal;
            }

            // Довго в повітрі над прірвою — подвійний стрибок (з запису: так перелітають до ~30 одиниць).
            if (!data.isGrounded && !data.isWallGrab && data.sinceGrounded > AirJumpAfter && data.currentJumps > 0
                && !data.ThereIsGroundBelow(myPosition, SafeDropDepth))
            {
                jump = true;
            }

            bool wantsUp = toTarget.y > ClimbHeight;
            if (data.isWallGrab)
            {
                // Як людина: тиснемо в стіну і відстрибуємо, поки ціль вище.
                horizontal = lastHorizontal;
                jump |= wantsUp;
            }

            // Без випадкових стрибків: з запису видно, що людина стрибає ~28 разів/хв, а бот стрибав 132.
            if (UpdateStuck(horizontal))
            {
                jump = true;
            }

            return (horizontal, jump);
        }

        /// <summary>Рух до довільної точки (укриття) маршрутом A*. (0, false) — маршруту немає.</summary>
        public (int horizontal, bool jump) MoveTo(Vector2 myPosition, Vector2 toGoal)
        {
            Vector2 escape = HazardCache.EscapeDirection(myPosition, HazardEscapeMargin);
            if (escape != Vector2.zero || !TryFollowPath(myPosition, toGoal, out int horizontal, out bool jump))
            {
                return (0, false);
            }

            if (horizontal != 0)
            {
                lastHorizontal = horizontal;
            }

            bool stuck = UpdateStuck(horizontal);
            return (horizontal, jump || (stuck && !airborneNow));
        }

        private int ChooseDirection(Vector2 myPosition, Vector2 toTarget, bool canSee, BotSettings settings)
        {
            // Ціль під нашою платформою і її не видно — йдемо до найближчого краю, з якого можна спуститися.
            if (!canSee && toTarget.y < -DetourHeight)
            {
                int edge = FindDropEdge(myPosition, toTarget.x);
                if (edge != 0)
                {
                    return edge;
                }
            }

            // Ціль над нами за стелею — йдемо туди, де над головою відкрито, і застрибуємо.
            if (!canSee && toTarget.y > ClimbHeight)
            {
                int open = FindOpenSky(myPosition, toTarget);
                if (open != 0)
                {
                    return open;
                }
            }

            return BotMath.Horizontal(toTarget.x, toTarget.magnitude, settings.PreferredDistance, canSee);
        }

        /// <summary>
        /// Шукає в обидва боки найближче місце, де вгору до висоти цілі немає перешкод.
        /// 0 — якщо просто над нами вже відкрито або нічого не знайдено.
        /// </summary>
        private int FindOpenSky(Vector2 myPosition, Vector2 toTarget)
        {
            float height = Mathf.Min(toTarget.y + 2f, 25f);
            LayerMask mask = PlayerManager.instance.canSeePlayerMask;
            if (!IsCeilingAbove(myPosition, height, mask))
            {
                return 0;
            }

            int preferred = toTarget.x >= 0f ? 1 : -1;
            foreach (int step in Enumerable.Range(1, EdgeSearchSteps))
            {
                foreach (int direction in new[] { preferred, -preferred })
                {
                    Vector2 probe = myPosition + new Vector2(direction * step, 0f);
                    if (!IsCeilingAbove(probe, height, mask))
                    {
                        return direction;
                    }
                }
            }

            return 0;
        }

        private static bool IsCeilingAbove(Vector2 from, float height, LayerMask mask)
        {
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(from, Vector2.up, height, mask))
            {
                Transform root = hit.transform != null ? hit.transform.root : null;
                if (root != null && root.GetComponent<Player>() == null && root.GetComponent<SpawnedAttack>() == null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Шукає в обидва боки найближчий край платформи, під яким є земля (або хоча б відкритий спуск).</summary>
        private int FindDropEdge(Vector2 myPosition, float targetDx)
        {
            int preferred = targetDx >= 0f ? 1 : -1;
            int best = 0;
            int bestSteps = int.MaxValue;

            foreach (int direction in new[] { preferred, -preferred })
            {
                for (int step = 1; step <= EdgeSearchSteps; step++)
                {
                    Vector2 probe = myPosition + new Vector2(direction * step, 0f);
                    if (data.ThereIsGroundBelow(probe, FlatGroundCheck))
                    {
                        continue;
                    }

                    // Знайшли край; бонус тому боку, де ціль.
                    int cost = step + (direction == preferred ? 0 : 3);
                    if (cost < bestSteps)
                    {
                        bestSteps = cost;
                        best = direction;
                    }

                    break;
                }
            }

            return best;
        }

        /// <summary>Стан маршруту для діагностики: довжина, перші кроки, запланований рух.</summary>
        /// <summary>
        /// Під час кроку Climb: чи натиснути стрибок у цей кадр — відразу після зачіпу за стіну (вікно гри 0.1 с)
        /// або подвійним стрибком на вершині, якщо ще нижче за край стіни.
        /// </summary>
        public bool ClimbJumpNow(Vector2 myPosition)
        {
            if (!plannedStep.HasValue || plannedStep.Value.Move != NavMove.Climb || data.isGrounded)
            {
                return false;
            }

            Vector2 landing = NavMap.ToWorld(plannedStep.Value.X, plannedStep.Value.Y);
            bool vyKnown = data.playerVel != null;
            float vy = vyKnown ? Velocity(data.playerVel).y : 0f;
            return BotMath.ClimbJump(data.sinceWallGrab, data.sinceJump, data.currentJumps, vy, myPosition.y, landing.y);
        }

        public string DescribePlan()
        {
            string planned = plannedStep.HasValue ? $"{plannedStep.Value.Move}->({plannedStep.Value.X},{plannedStep.Value.Y})" : "-";
            string steps = LastPath == null ? "-" : string.Join(" ", LastPath.Take(5).Select(s => $"{s.Move}({s.X},{s.Y})"));
            return $"path={LastPathLength} planned={planned} next=[{steps}] grounded={data.isGrounded} wall={data.isWallGrab}";
        }

        private System.Collections.Generic.List<NavStep> LastPath;

        /// <summary>Довжина останнього маршруту A* (0 — маршрут не використовувався). Для лога.</summary>
        public int LastPathLength { get; private set; }

        private bool TryFollowPath(Vector2 myPosition, Vector2 toTarget, out int horizontal, out bool jump)
        {
            horizontal = 0;
            jump = false;

            bool airborne = !data.isGrounded && !data.isWallGrab;
            if (airborne && !airborneNow)
            {
                airborneSince = Time.time; // відрив від землі — з нього рахуємо вікно довіри стрибку
            }

            airborneNow = airborne;
            if (!airborne)
            {
                airJumpUsed = false;
            }

            // Взятий зіскок/стрибок виконуємо до кінця — інакше на краю бот перераховує маршрут
            // з клітинки внизу і смикається туди-сюди.
            if (plannedStep.HasValue && plannedStep.Value.Move != NavMove.Walk
                && Time.time - plannedAt < CommitSeconds(plannedStep.Value.Move))
            {
                NavStep step = plannedStep.Value;
                Vector2 landing = NavMap.ToWorld(step.X, step.Y);
                bool arrived = !airborne && Mathf.Abs(landing.x - myPosition.x) < 0.8f && Mathf.Abs(landing.y - myPosition.y) < 1.5f;
                if (!arrived)
                {
                    ExecuteCommittedStep(step, landing, myPosition, airborne, out horizontal, out jump);
                    lastAirY = myPosition.y;
                    return true;
                }
            }

            plannedStep = null;
            lastAirY = myPosition.y;

            // Плануємо і в повітрі (відкидання, висіння біля стіни): старт — клітинка під нами.
            NavGrid grid = NavMap.Get();
            (int cx, int cy) = NavMap.ToCell(myPosition);
            (int tx, int ty) = NavMap.ToCell(myPosition + toTarget);
            if (TryLeaveRoof(grid, cx, cy, out horizontal))
            {
                return true;
            }

            if (!TryFindStartCell(grid, cx, cy, out int sx, out int sy) || !grid.TryFindStandable(tx, ty, out int gx, out int gy))
            {
                LastPathLength = -1; // у лозі: -1 — немає клітинки старту/цілі, 0 — немає маршруту
                return false;
            }

            EvaluateAttempt(sx, sy, myPosition);

            var path = grid.FindPath(sx, sy, gx, gy);
            if (path.Count == 0 && TryNearestReachable(grid, sx, sy, gx, gy, myPosition + toTarget, out int nx, out int ny))
            {
                // До цілі не дістатися — йдемо в досяжну клітинку, найближчу до неї (звідти може бути лінія вогню).
                path = grid.FindPath(sx, sy, nx, ny);
            }

            LastPathLength = path.Count;
            LastPath = path;
            if (path.Count == 0)
            {
                BreakTarget = null;
                plannedStep = null;
                return false;
            }

            NavStep next = path[0];
            BreakTarget = null;
            if (next.Move == NavMove.Break)
            {
                return BeginBreak(grid, sx, sy, next, out horizontal, out jump);
            }

            if (next.Move == NavMove.Bounce)
            {
                bounceColumn = grid.FindBounceColumn(sx, sy, next.X, next.Y);
                bounced = false;
                if (bounceColumn < 0 || data.block == null || data.block.IsOnCD())
                {
                    // Без готового блоку відскок коштує 51 HP. Раніше бот просто стояв і чекав блок
                    // (гра 2026-11-25: «пробіл у пошуку шляху») — спершу шукаємо дорогу без відскоку.
                    var noBounce = grid.FindPath(sx, sy, gx, gy, allowBounce: false);
                    if (noBounce.Count == 0)
                    {
                        plannedStep = null;
                        return true;
                    }

                    path = noBounce;
                    LastPathLength = path.Count;
                    LastPath = path;
                    next = path[0];
                }
            }

            StartAttempt(sx, sy, next);
            int stepDx = next.X - sx;
            horizontal = stepDx > 0 ? 1 : stepDx < 0 ? -1 : 0;

            // Крок угору по ходьбі, а попереду крута стінка/сходинка — підстрибуємо (гра 2026-11-25, 3e40b284:
            // бот упирався в сходинку на 1 клітинку, розвертався й ходив туди-сюди).
            if (next.Move == NavMove.Walk && next.Y > sy && !airborne && horizontal != 0 && IsWallAhead(myPosition, horizontal))
            {
                jump = true;
            }

            if (next.Move != NavMove.Walk)
            {
                plannedStep = next;
                plannedAt = Time.time;

                // У повітрі стрибнути можна лише від стіни або подвійним стрибком.
                if (next.Move == NavMove.Climb)
                {
                    bool hasWall = grid.TryGetClimbWall(sx, sy, next.X, out int column, out climbWallDirection);
                    climbColumnX = hasWall ? NavMap.ToWorld(column, sy).x : (float?)null;
                    horizontal = WallSide(myPosition, horizontal);
                    jump = !airborne || data.isWallGrab;
                }
                else if (next.Move == NavMove.Jump)
                {
                    if (!airborne || data.isWallGrab)
                    {
                        jump = true;
                    }
                    else if (!airJumpUsed && CanJumpInAir)
                    {
                        jump = true;
                        airJumpUsed = true;
                    }
                }
            }

            return true;
        }

        /// <summary>Руйнівна клітинка, яку треба прострілити на маршруті (BotBrain стріляє, поки немає пострілу по ворогу).</summary>
        public Vector2? BreakTarget { get; private set; }

        private float? climbColumnX;
        private int climbWallDirection;

        private const float BreakGiveUpSeconds = 8f;
        private (int fx, int fy, int tx, int ty) breakEdge = (-1, -1, -1, -1);
        private float breakSince;

        /// <summary>
        /// Крок «пробити»: стоїмо на місці, BotBrain прострілює шар. Коли дірка з'явиться, сітка перебудується, і
        /// маршрут піде звичайним стрибком/ходьбою. Не пробивається за <see cref="BreakGiveUpSeconds"/> — штраф переходу.
        /// </summary>
        private bool BeginBreak(NavGrid grid, int sx, int sy, NavStep next, out int horizontal, out bool jump)
        {
            horizontal = 0;
            jump = false;
            plannedStep = null;
            var edge = (sx, sy, next.X, next.Y);
            if (edge != breakEdge)
            {
                breakEdge = edge;
                breakSince = Time.time;
            }
            else if (Time.time - breakSince > BreakGiveUpSeconds)
            {
                NavMap.ReportFailure(sx, sy, next.X, next.Y, "Break");
                breakSince = Time.time;
                return true;
            }

            if (grid.TryGetBreakCell(sx, sy, next.X, next.Y, out int bx, out int by))
            {
                BreakTarget = NavMap.ToWorld(bx, by);
            }

            return true;
        }

        /// <summary>
        /// Бот на даху над сіткою (вище верхнього ряду, під ним тверде — гра 2026-11-25, bd7e42cf: стояв на верхній плиті
        /// карти, а маршрут рахувався «під стелею» й ходив по колу): ідемо до найближчого краю даху й зістрибуємо.
        /// </summary>
        private bool TryLeaveRoof(NavGrid grid, int cx, int cy, out int horizontal)
        {
            horizontal = 0;
            int top = grid.Height - 1;
            if (cy < top || !grid.IsSolid(Mathf.Clamp(cx, 0, grid.Width - 1), top))
            {
                return false;
            }

            for (int distance = 1; distance < grid.Width; distance++)
            {
                bool left = cx - distance >= 0 && !grid.IsSolid(cx - distance, top);
                bool right = cx + distance < grid.Width && !grid.IsSolid(cx + distance, top);
                if (left || right)
                {
                    horizontal = right && (!left || lastHorizontal >= 0) ? 1 : -1;
                    return true;
                }
            }

            return false;
        }

        private const float NearestReachableBudget = 200f;
        private const float NearestReachableCacheSeconds = 1f;
        private const float ApproachSightRange = 30f;
        private (int sx, int sy, int gx, int gy) nearestKey;
        private (int x, int y) nearestCell;
        private float nearestAt = float.NegativeInfinity;

        /// <summary>Досяжна зі старту клітинка, найближча до цілі (кеш на секунду — Dijkstra по всій карті).</summary>
        private (int x, int y) approachCell = (-1, -1);
        private (int x, int y) approachTargetCell = (-1, -1);

        private bool TryNearestReachable(NavGrid grid, int sx, int sy, int gx, int gy, Vector2 targetWorld, out int nx, out int ny)
        {
            var key = (sx, sy, gx, gy);
            if (key != nearestKey || Time.time - nearestAt > NearestReachableCacheSeconds)
            {
                nearestKey = key;
                nearestAt = Time.time;
                nearestCell = (sx, sy);
                // Гравець сховався (гра 2026-11-25: бот хвилину стояв під гравцем у кишені): шукаємо досяжну
                // клітинку, звідки ціль ВИДНО, — а не найближчу по прямій.
                var reachable = grid.ReachableWithin(sx, sy, NearestReachableBudget);
                var options = new System.Collections.Generic.List<(float cost, bool canSee, float distanceToTarget)>(reachable.Count);
                LayerMask mask = PlayerManager.instance.canSeePlayerMask;
                foreach ((int x, int y, float cost) in reachable)
                {
                    Vector2 eye = NavMap.ToWorld(x, y) + Vector2.up * 0.5f;
                    float distance = Vector2.Distance(eye, targetWorld);
                    bool canSee = distance <= ApproachSightRange && !Physics2D.Linecast(eye, targetWorld, mask);
                    options.Add((cost, canSee, distance));
                }

                int previous = reachable.FindIndex(c => c.x == approachCell.x && c.y == approachCell.y);
                int chosen = BotMath.ChooseApproachCell(options, approachTargetCell == (gx, gy) ? previous : -1);
                if (chosen >= 0)
                {
                    nearestCell = (reachable[chosen].x, reachable[chosen].y);
                    approachCell = nearestCell;
                    approachTargetCell = (gx, gy);
                }
            }

            nx = nearestCell.x;
            ny = nearestCell.y;
            return nearestCell != (sx, sy);
        }

        private void StartAttempt(int sx, int sy, NavStep next)
        {
            if (attempt.HasValue && attempt.Value.fx == sx && attempt.Value.fy == sy && attempt.Value.tx == next.X && attempt.Value.ty == next.Y)
            {
                return; // той самий крок — не скидаємо таймер
            }

            float lowY = Mathf.Min(NavMap.ToWorld(sx, sy).y, NavMap.ToWorld(next.X, next.Y).y) - FellBelow;
            attempt = (sx, sy, next.X, next.Y, next.Move, Time.time, lowY, NavMap.Generation);
        }

        /// <summary>Рядок JUMP ok/fail — справжня частка провалів стрибків і їхня довжина.</summary>
        private void LogJump(string result, int fx, int fy, int tx, int ty, NavMove move, float startedAt)
        {
            BotLog.Write(System.FormattableString.Invariant(
                $"JUMP {result} bot={data.player.playerID} move={move} dx={tx - fx} dy={ty - fy} t={Time.time - startedAt:0.00}"));
        }

        /// <summary>Дійшли — успіх; впали нижче за обидві клітинки або не дійшли за таймаут — невдача, штраф переходу.</summary>
        private void EvaluateAttempt(int sx, int sy, Vector2 myPosition)
        {
            if (!attempt.HasValue)
            {
                return;
            }

            var a = attempt.Value;
            if (a.generation != NavMap.Generation || data.dead)
            {
                attempt = null; // нове очко/мапа або смерть — спроба не оцінюється
                return;
            }

            if (data.healthHandler != null && LastDamaged(data.healthHandler) >= a.at)
            {
                attempt = null; // збила куля — маршрут не винен
                return;
            }

            if (sx == a.tx && sy == a.ty)
            {
                if (a.move == NavMove.Jump || a.move == NavMove.Fall)
                {
                    NavMap.ReportSuccess(a.fx, a.fy, a.tx, a.ty);
                    LogJump("ok", a.fx, a.fy, a.tx, a.ty, a.move, a.at);
                }

                attempt = null; // вдалося
                return;
            }

            bool fell = a.move != NavMove.Bounce && myPosition.y < a.lowY;   // відскок — навмисне падіння
            bool timedOut = Time.time - a.at > (a.move == NavMove.Walk || a.move == NavMove.Fall || a.move == NavMove.Jump
                ? AttemptTimeout
                : CommitSeconds(a.move) + 1f);
            if (fell || timedOut)
            {
                string why = fell ? "fell" : "timeout";
                if (BotMath.ShouldPenalise(a.move, fell))
                {
                    NavMap.ReportFailure(a.fx, a.fy, a.tx, a.ty, $"{a.move}/{why}");
                }
                else if (BotMath.ShouldPenaliseTimeout(a.move, fell))
                {
                    NavMap.ReportTimeout(a.fx, a.fy, a.tx, a.ty, $"{a.move}/{why}");
                }

                if (a.move == NavMove.Jump || a.move == NavMove.Fall)
                {
                    LogJump("fail/" + why, a.fx, a.fy, a.tx, a.ty, a.move, a.at);
                }

                attempt = null;
                return;
            }

            if (sx != a.fx || sy != a.fy)
            {
                attempt = null; // кудись перейшли (не туди, але й не впали) — нейтрально
            }
        }

        /// <summary>
        /// Клітинка старту: спершу поточна й сусідні на тому ж рівні (на краю платформи центр може бути
        /// вже над прірвою, хоча ноги ще стоять), і лише потім — скан донизу.
        /// </summary>
        private static bool TryFindStartCell(NavGrid grid, int cx, int cy, out int sx, out int sy)
        {
            foreach ((int ox, int oy) in StartCellOffsets)
            {
                if (grid.IsStandable(cx + ox, cy + oy))
                {
                    sx = cx + ox;
                    sy = cy + oy;
                    return true;
                }
            }

            return grid.TryFindStandable(cx, cy, out sx, out sy);
        }

        private void ExecuteCommittedStep(NavStep step, Vector2 landing, Vector2 myPosition, bool airborne, out int horizontal, out bool jump)
        {
            float dx = landing.x - myPosition.x;
            horizontal = Mathf.Abs(dx) > 0.3f ? (dx > 0f ? 1 : -1) : 0;
            jump = false;

            if (step.Move == NavMove.Bounce)
            {
                ExecuteBounce(landing, myPosition, out horizontal);
                return;
            }

            if (step.Move == NavMove.Climb)
            {
                // Як людина: поки нижче полиці — тиснемо в стіну й відстрибуємо щоразу, як зачепилися;
                // коли вже вище — летимо на полицю.
                if (myPosition.y < landing.y - 0.3f)
                {
                    int side = WallSide(myPosition, horizontal != 0 ? horizontal : climbWallDirection);
                    if (!IsWallAhead(myPosition, side) && !data.isWallGrab && climbColumnX.HasValue)
                    {
                        // Стіни ще не видно (вона вище, навис, кут торта): спершу у свою колонку, далі тиснемо в бік стіни
                        // зі сітки — так зачепимося, щойно дістанемо (гра 2026-11-25: бот стояв під полицею з dir=0).
                        float toColumn = climbColumnX.Value - myPosition.x;
                        side = Mathf.Abs(toColumn) > 0.45f ? (toColumn > 0f ? 1 : -1) : climbWallDirection;
                    }

                    horizontal = side;
                    jump = !airborne || data.isWallGrab;
                }

                return;
            }

            if (step.Move != NavMove.Jump)
            {
                return; // зіскок: просто йдемо/летимо до точки приземлення
            }

            if (!airborne)
            {
                jump = true; // ще не відірвалися від землі — стрибаємо
                return;
            }

            // Поки летимо вгору (чи нижче за платформу) — не тиснемо в стінку попереду: спершу вище за неї, потім убік
            // (гра 2026-11-25, свічка на торті: бот впирався в стінку й зачіплявся за неї замість перелетіти).
            bool risingNow = data.playerVel != null && Velocity(data.playerVel).y > 0f;
            if ((risingNow || myPosition.y < landing.y - 1f) && horizontal != 0 && IsWallAhead(myPosition, horizontal))
            {
                horizontal = 0;
            }

            // Подвійний стрибок — лише на вершині чи при падінні і якщо ще нижче за ціль.
            bool falling = myPosition.y <= lastAirY;
            if (!airJumpUsed && CanJumpInAir && falling && myPosition.y < landing.y + StandingOffset && data.sinceGrounded > AirJumpAfter)
            {
                jump = true;
                airJumpUsed = true;
            }
        }

        private static float CommitSeconds(NavMove move)
        {
            return move == NavMove.Climb ? MaxClimbSeconds : move == NavMove.Bounce ? MaxBounceSeconds : MaxCommitSeconds;
        }

        /// <summary>
        /// Відскок: до межі — йдемо/падаємо в колону падіння (блок об межу ставить BotBrain щокадру);
        /// після відскоку тримаємося колони, поки нижче за полицю, потім летимо на неї.
        /// </summary>
        private void ExecuteBounce(Vector2 landing, Vector2 myPosition, out int horizontal)
        {
            if (myPosition.y < BouncedBelowY)
            {
                bounced = true;
            }

            float columnX = bounceColumn >= 0 ? NavMap.ToWorld(bounceColumn, 0).x : landing.x;
            float targetX = bounced && myPosition.y > landing.y + 0.5f ? landing.x : columnX;
            float dx = targetX - myPosition.x;
            horizontal = Mathf.Abs(dx) > 0.3f ? (dx > 0f ? 1 : -1) : 0;
            if (horizontal != 0)
            {
                horizontal = BotMath.BounceFallHorizontal(horizontal, bounced, IsWallAhead(myPosition, horizontal));
            }
        }

        /// <summary>Бік, де стіна для лазіння (спершу той, куди йдемо); якщо стін поруч немає — fallback.</summary>
        private static int WallSide(Vector2 position, int fallback)
        {
            int preferred = fallback != 0 ? fallback : 1;
            if (IsWallAhead(position, preferred))
            {
                return preferred;
            }

            return IsWallAhead(position, -preferred) ? -preferred : fallback;
        }

        private static bool IsWallAhead(Vector2 from, int horizontal)
        {
            LayerMask mask = PlayerManager.instance.canSeePlayerMask;
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(from, new Vector2(horizontal, 0f), WallProbeDistance, mask))
            {
                Transform root = hit.transform != null ? hit.transform.root : null;
                if (root != null && root.GetComponent<Player>() == null && root.GetComponent<SpawnedAttack>() == null
                    && BotMath.IsSteepWall(hit.normal.x, hit.normal.y))
                {
                    return true; // похилий схил — не стіна (гра 2026-11-25: бот гальмував і розвертався на схилах)
                }
            }

            return false;
        }

        private bool IsEdgeAhead(Vector2 myPosition, int horizontal)
        {
            return !data.ThereIsGroundBelow(myPosition + new Vector2(horizontal * LookAhead, 0f), FlatGroundCheck);
        }

        private bool HasGroundFarBelow(Vector2 myPosition, int horizontal)
        {
            return data.ThereIsGroundBelow(myPosition + new Vector2(horizontal * LookAhead, 0f), SafeDropDepth);
        }

        private bool HasLandingAhead(Vector2 myPosition, int horizontal)
        {
            foreach (float distance in GapLandingDistances)
            {
                Vector2 probe = myPosition + new Vector2(horizontal * distance, 2f);
                if (data.ThereIsGroundBelow(probe, SafeDropDepth) && !HazardCache.IsDangerous(probe))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Чи є маршрут A* від бота до точки (без запасного «найближча досяжна клітинка»).</summary>
        public bool HasRouteTo(Vector2 myPosition, Vector2 goal)
        {
            NavGrid grid = NavMap.Get();
            (int cx, int cy) = NavMap.ToCell(myPosition);
            (int tx, int ty) = NavMap.ToCell(goal);
            return TryFindStartCell(grid, cx, cy, out int sx, out int sy)
                && grid.TryFindStandable(tx, ty, out int gx, out int gy)
                && grid.FindPath(sx, sy, gx, gy).Count > 0;
        }

        /// <summary>Коли бот востаннє «застряг» (для RESULT died: чи застрягав перед смертю).</summary>
        public float LastStuckAt => lastStuckAt;

        private bool UpdateStuck(int horizontal)
        {
            float now = Time.time;
            float elapsed = Mathf.Clamp(now - lastThinkTime, 0.0001f, MaxStuckElapsed);
            lastThinkTime = now;

            float x = data.transform.position.x;
            float speed = Mathf.Abs(x - lastX) / elapsed;
            lastX = x;

            stuckTimer = horizontal != 0 && speed < StuckSpeed ? stuckTimer + elapsed : 0f;
            if (stuckTimer < StuckSeconds)
            {
                return false;
            }

            stuckTimer = 0f;
            Vector2 at = data.transform.position;
            bool sameSpot = now - lastStuckAt < SameSpotWindow && Vector2.Distance(at, lastStuckPosition) < SameSpotDistance;
            sameSpotCount = sameSpot ? sameSpotCount + 1 : 0;
            if (horizontal != 0 && BotMath.ShouldReverseWhenStuck(now - lastStuckAt < RepeatedStuckWindow, sameSpot, data.isGrounded))
            {
                // Кут чи виступ (гра 2026-11-25: бот 20 с висів в одній точці): розворот одразу, і що частіше — то довше.
                reverseDirection = -horizontal;
                reverseUntil = now + BotMath.StuckReverseSeconds(ReverseSeconds, sameSpotCount);
            }

            lastStuckAt = now;
            lastStuckPosition = at;
            BotLog.Write(System.FormattableString.Invariant($"STUCK bot={data.player.playerID} reason=no-progress-x move={horizontal} pos=({at.x:F1},{at.y:F1}) grounded={data.isGrounded} wall={data.isWallGrab} reversed={reverseUntil > now}"));
            return true;
        }
    }
}
