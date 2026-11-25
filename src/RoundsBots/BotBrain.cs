using System.Collections.Generic;
using System.Linq;
using RoundsBots.Logic;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Керує гравцем-ботом: щокадру записує рішення в <see cref="GeneralInput"/>, як це робив би контролер.
    /// Рішення (ціль, приціл, рух) оновлюються раз на <see cref="BotSettings.ThinkInterval"/>,
    /// а реакції, де важить кадр (блок об межу арени), перевіряються щокадру.
    /// </summary>
    public sealed class BotBrain : MonoBehaviour
    {
        private const float BulletHitRadius = 2f;
        private const float HomingBulletHitRadius = 4f; // самонавідна куля довертає — небезпечна зона ширша
        private const float BlockReactionWindow = 0.3f;
        private const float JumpHoldSeconds = 0.25f;

        /// <summary>Приблизна гравітація гравця (од/с²) для прогнозу верхньої точки стрибка.</summary>
        private const float CeilingGravityEstimate = 40f;

        private float lastVerticalSpeed;

        /// <summary>Справжня швидкість гравця. Швидкість «за кадр» при 256 FPS у кілька разів завищена (позиція
        /// змінюється лише на кроках фізики) — через це блок об межу арени натискався зарано (гра 2026-11-25).</summary>
        private static readonly HarmonyLib.AccessTools.FieldRef<PlayerVelocity, Vector2> RealVelocity =
            HarmonyLib.AccessTools.FieldRefAccess<PlayerVelocity, Vector2>("velocity");
        private const float BoundsBlockLookAhead = 0.08f;
        private const float DebugLogInterval = 3f;
        private const float ArenaMargin = 10f;
        private const float MinReloadToHide = 0.5f;     // коротка перезарядка — ховатися немає сенсу
        private const float HideFromDistance = 30f;     // ворог далі — він і так не загроза
        private const float RetreatDistance = 30f;      // без укриття — просто відходимо
        private const float BreakableProbeDistance = 3f;
        private const float CloseBlockDistance = 4f;    // з картами на блок — блокуємо поруч з ворогом
        private const float LoadoutRefreshSeconds = 1f;
        private const float CloseBlockInterval = 2.5f;  // не палимо блок щойно він відкотився
        private const int MaxShotsPerObstacle = 6;      // перегородка не ламається — не стріляємо вічно
        private const float MovableJumpDistance = 1.5f; // впритул до ящика/кулі — стрибаємо

        private static readonly string[] VanillaAiTypes =
        {
            "PlayerAI", "PlayerAIPhilip", "PlayerAIDavid", "PlayerAIPetter", "PlayerAIWilhelm", "PlayerAIZorro",
        };

        private CharacterData data;
        private BotAiming aiming;
        private BotNavigation navigation;
        private BotLoadout loadout;
        private readonly BotCover cover = new BotCover();
        private int closeRangeEffects;
        private float loadoutRefreshAt;
        private bool wasDead;
        private float lastClearShotAt;
        private float? clearShotSince;
        private bool approaching;
        private string stalemateReason;
        private Player routeOverride;
        private float? noRouteSince;
        private float nextRouteCheckAt;
        private float aliveSince;
        private float nextCloseBlockAt;
        private Vector2 obstaclePoint;
        private int obstacleShots;
        private float thinkTimer;
        private float jumpHoldTimer;

        private Vector2 moveDirection;
        private Vector2 aimDirection = Vector2.right;
        private bool shooting;
        private bool wasShooting;
        private bool pendingJump;
        private bool pendingBlock;
        private Vector2 lastFramePosition;
        private float nextDebugLogAt;
        private bool couldActLastFrame;

        public static bool IsBot(Player player) => player != null && player.GetComponent<BotBrain>() != null;

        private void Awake()
        {
            data = GetComponent<CharacterData>();
            aiming = new BotAiming(data);
            navigation = new BotNavigation(data);
        }

        private void Start()
        {
            DisableVanillaAi();
            loadout = new BotLoadout(data);
            lastFramePosition = transform.position;
            Plugin.Log.LogInfo($"Bot brain attached to player {data.player.playerID} (team {data.player.teamID}).");
        }

        private void Update()
        {
            if (data == null || data.view == null || !data.view.IsMine)
            {
                return;
            }

            GeneralInput input = data.input;
            input.ResetInput();

            Vector2 position = transform.position;
            Vector2 frameVelocity = data.playerVel != null
                ? RealVelocity(data.playerVel)
                : (position - lastFramePosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastVerticalSpeed = frameVelocity.y;
            lastFramePosition = position;

            ReportDeath();
            bool canAct = CanAct();
            if (canAct && !couldActLastFrame)
            {
                aliveSince = Time.time;
                lastClearShotAt = Time.time;
                approaching = false;
                routeOverride = null;
                noRouteSince = null;
                NavMap.Invalidate(); // почалося нове очко — можливо, нова карта
            }

            couldActLastFrame = canAct;
            if (!canAct)
            {
                // Пауза, смерть, вибір карток — скидаємо стан, щоб після повернення не було хибного «застряг».
                navigation.Reset();
                shooting = false;
                wasShooting = false;
                jumpHoldTimer = 0f;
                return;
            }

            BotSettings settings = Plugin.Settings;
            thinkTimer -= Time.deltaTime;
            if (thinkTimer <= 0f)
            {
                thinkTimer = settings.ThinkInterval * Random.Range(0.8f, 1.2f);
                Think(settings);
            }

            // Удар об межу арени з блоком — без шкоди і з сильним відскоком.
            if (BotMath.WillHitArenaBounds(position.x, position.y, frameVelocity.x, frameVelocity.y, BoundsBlockLookAhead)
                && data.block != null && !data.block.IsOnCD())
            {
                pendingBlock = true;
            }

            ApplyInput(input);
        }

        private void OnDisable()
        {
            // Мертвого гравця гра вимикає — Update уже не викличеться, тож смерть фіксуємо тут.
            if (data != null && data.dead && !wasDead)
            {
                ReportDeath();
            }
        }

        private void ApplyInput(GeneralInput input)
        {
            input.direction = moveDirection;
            input.aimDirection = aimDirection;

            bool canShoot = shooting && !input.silencedInput;
            input.shootIsPressed = canShoot;
            input.shootWasPressed = canShoot;
            input.shootWasReleased = wasShooting && !canShoot; // потрібно для зброї із зарядом
            wasShooting = canShoot;

            // Лазіння по стіні — щокадру, а не лише в «думанні»: гра дає відстрибнути від стіни лише 0.1 с після
            // зачіпу, і бот пропускав це вікно та зісковзував (гра 2026-11-25: «не вистачає стрибка»).
            if (navigation.ClimbJumpNow(transform.position))
            {
                pendingJump = true;
            }

            // Стеля (верхня межа арени): з високої платформи звичайний стрибок виносить за межу — смерть.
            // Біля стелі — короткий стрибок без утримання; під самою межею — не стрибаємо.
            bool shortJump = false;
            if (pendingJump && BotMath.JumpWouldHitCeiling(transform.position.y))
            {
                shortJump = true;
                pendingJump = !BotMath.JumpWouldHitCeiling(transform.position.y, BotMath.ShortJumpApexEstimate);
            }

            if (jumpHoldTimer > 0f && BotMath.ShouldReleaseJump(transform.position.y, lastVerticalSpeed, CeilingGravityEstimate))
            {
                jumpHoldTimer = 0f;
            }

            if (pendingJump)
            {
                input.jumpWasPressed = true;
                jumpHoldTimer = shortJump ? 0f : JumpHoldSeconds;
                pendingJump = false;
            }

            // Тримаємо стрибок трохи довше — так стрибок вищий, як у людини.
            if (jumpHoldTimer > 0f)
            {
                input.jumpIsPressed = true;
                jumpHoldTimer -= Time.deltaTime;
            }

            if (pendingBlock && !input.silencedInput)
            {
                input.shieldWasPressed = true;
            }

            pendingBlock = false;
        }

        private bool CanAct()
        {
            // Між раундами гравців «паркують» за картою (y≈200) — там нічого не робимо, щоб не витрачати блок і кулі.
            Vector2 position = transform.position;
            bool insideArena = Mathf.Abs(position.x) < BotMath.ArenaHalfWidth + ArenaMargin
                && Mathf.Abs(position.y) < BotMath.ArenaHalfHeight + ArenaMargin;

            return insideArena
                && data.isPlaying
                && !data.dead
                && !data.isStunned
                && !data.input.stunnedInput
                && !GameManager.lockInput;
        }

        private void Think(BotSettings settings)
        {
            Vector2 myPosition = transform.position;
            Player target = FindTarget(myPosition);
            if (target == null)
            {
                moveDirection = Vector2.zero;
                shooting = false;
                return;
            }

            Vector2 toTarget = target.data.playerVel.position - myPosition;
            bool canSee = PlayerManager.instance.CanSeePlayer(myPosition, target).canSee;

            aimDirection = aiming.Aim(myPosition, target, settings.AimErrorDegrees);
            shooting = aiming.HasClearShot && Random.value < settings.ShootChance;
            // Ванільна перевірка суворіша за реальну лінію вогню (лог: canSee=False, clearShot=True). Але рикошет чи
            // самонаведення — не «бачу»: інакше бот сидить під платформою цілі й стріляє в стелю замість обійти.
            canSee |= aiming.HasClearShot && !aiming.LastBankShot && !aiming.LastHomingShot;
            settings = AdaptToLoadout(settings);
            settings = BreakStalemate(settings, ref canSee);

            float reload = loadout.ReloadRemaining();
            // Уже в укритті — сидимо там до кінця перезарядки, навіть якщо ворога не видно.
            bool hiding = !IsDisabled(BotExperiment.NoHide) && reload > MinReloadToHide && (canSee || cover.Current.HasValue) && toTarget.magnitude < HideFromDistance;
            (int horizontal, bool jump) = hiding
                ? Hide(myPosition, toTarget, target, settings)
                : navigation.Decide(myPosition, toTarget, canSee, settings);
            if (!hiding)
            {
                cover.Clear();
            }

            moveDirection = new Vector2(horizontal, 0f);
            pendingJump |= jump;

            if (!IsDisabled(BotExperiment.NoBreakObstacles))
            {
                HandleObstacleAhead(myPosition, horizontal, canShoot: !aiming.HasClearShot && reload <= 0f);
            }

            UpdateBlock(myPosition, settings);
            if (closeRangeEffects > 0 && !IsDisabled(BotExperiment.NoCloseBlock) && canSee && toTarget.magnitude < CloseBlockDistance
                && Time.time >= nextCloseBlockAt && !pendingBlock && Random.value < settings.BlockChance
                && data.block != null && !data.block.IsOnCD())
            {
                pendingBlock = true; // ударна хвиля / поле на блок діють лише поруч
                nextCloseBlockAt = Time.time + CloseBlockInterval;
            }

            LastCanSee = canSee;
            LastClearShot = aiming.HasClearShot;
            TrackRouteLoop(myPosition, toTarget);

            if (Time.time >= nextDebugLogAt)
            {
                nextDebugLogAt = Time.time + DebugLogInterval;
                BotLog.Write(
                    $"Bot {data.player.playerID}: pos={myPosition:F1} toTarget={toTarget:F1} canSee={canSee} clearShot={aiming.HasClearShot} " +
                    $"bullet(speed={aiming.LastBulletSpeed:F0}, g={aiming.LastBulletGravity:F0}) bank={aiming.LastBankShot} homing={aiming.LastHomingShot} move={horizontal} path={navigation.LastPathLength} grounded={data.isGrounded} wall={data.isWallGrab} " +
                    $"dist={settings.PreferredDistance:F0} close={closeRangeEffects} reload={reload:F1} hide={hiding} cover={cover.Current?.ToString("F1") ?? "-"}");
            }
        }

        private readonly RoundsBots.Logic.LoopDetector loopDetector = new RoundsBots.Logic.LoopDetector();
        private float nextLoopSampleAt;
        private const float LoopSampleSeconds = 0.5f;
        private const float LoopMinTargetDistance = 10f;

        /// <summary>Діагностика: бот 10 с кружляє на місці без пострілу, ціль далеко — рядок ROUTE_LOOP.</summary>
        private void TrackRouteLoop(Vector2 myPosition, Vector2 toTarget)
        {
            if (aiming.HasClearShot || toTarget.magnitude < LoopMinTargetDistance)
            {
                loopDetector.Clear();
                return;
            }

            if (Time.time < nextLoopSampleAt)
            {
                return;
            }

            nextLoopSampleAt = Time.time + LoopSampleSeconds;
            loopDetector.Add(Time.time, myPosition.x, myPosition.y);
            if (loopDetector.IsLooping(Time.time))
            {
                BotLog.Write($"ROUTE_LOOP bot={data.player.playerID} pos={myPosition:F1} toTarget={toTarget:F1} {navigation.DescribePlan()}");
                loopDetector.Clear();
            }
        }

        /// <summary>
        /// Пат (ШІ-чат v2, 2026-11-22): ціль видно, але пострілу немає 3 с — режим наближення: йдемо маршрутом
        /// (canSee=false для навігації); виходимо лише після 0,5 с безперервного пострілу (без смикання).
        /// Тиша 15+ с стискає бажану дистанцію — але тільки ботам у режимі наближення (хто може влучати, стоїть).
        /// </summary>
        private BotSettings BreakStalemate(BotSettings settings, ref bool canSee)
        {
            float now = Time.time;
            if (aiming.HasClearShot)
            {
                lastClearShotAt = now;
                clearShotSince = clearShotSince ?? now;
            }
            else
            {
                clearShotSince = null;
            }

            if (Stalemate.ShouldApproach(canSee, now - lastClearShotAt))
            {
                approaching = true;
            }
            else if (approaching && clearShotSince.HasValue && now - clearShotSince.Value >= Stalemate.ExitShotSeconds)
            {
                approaching = false;
            }

            float quiet = now - DamageTracker.LastDamageAt;
            string reason = routeOverride != null ? "noPath" : approaching ? (quiet > Stalemate.QuietSeconds ? "quiet" : "noShot") : null;
            if (reason != stalemateReason)
            {
                Vector2 at = transform.position;
                string change = reason != null ? "enter=" + reason : "exit=" + stalemateReason;
                BotLog.Write(System.FormattableString.Invariant(
                    $"STALEMATE bot={data.player.playerID} {change} quiet={quiet:F0} pos=({at.x:F1},{at.y:F1})"));
                stalemateReason = reason;
            }

            float distance = Stalemate.PreferredDistance(settings.PreferredDistance, quiet);
            if (!approaching)
            {
                // Стріляє, але 5+ с ніхто не влучає (Шидзен: «стріляв, але до мене не намагався дійти») — підходимо ближче.
                return Mathf.Approximately(distance, settings.PreferredDistance) ? settings : settings.WithPreferredDistance(distance);
            }

            canSee = false;
            return Mathf.Approximately(distance, settings.PreferredDistance) ? settings : settings.WithPreferredDistance(distance);
        }

        /// <summary>Бажана дистанція під поточну збірку (оновлюється раз на секунду, бо рахує компоненти).</summary>
        private BotSettings AdaptToLoadout(BotSettings settings)
        {
            if (IsDisabled(BotExperiment.NoLoadoutDistance))
            {
                return settings;
            }

            if (Time.time >= loadoutRefreshAt)
            {
                loadoutRefreshAt = Time.time + LoadoutRefreshSeconds;
                closeRangeEffects = loadout.CloseRangeEffects;
            }

            float distance = BotMath.PreferredDistance(settings.PreferredDistance, loadout.BulletRange(aiming.LastBulletSpeed), closeRangeEffects);
            return Mathf.Approximately(distance, settings.PreferredDistance) ? settings : settings.WithPreferredDistance(distance);
        }

        /// <summary>Перезарядка: біжимо в найближче місце, де ворог нас не бачить; немає — відходимо.</summary>
        private (int horizontal, bool jump) Hide(Vector2 myPosition, Vector2 toTarget, Player target, BotSettings settings)
        {
            Vector2? spot = cover.Find(myPosition, target);
            if (spot.HasValue)
            {
                (int horizontal, bool jump) = navigation.MoveTo(myPosition, spot.Value - myPosition);
                if (horizontal != 0 || jump || Vector2.Distance(spot.Value, myPosition) < 1f)
                {
                    return (horizontal, jump);
                }

                return (0, false); // маршрут тимчасово недоступний — краще стояти, ніж вибігати під кулі
            }

            return navigation.Decide(myPosition, toTarget, true, settings.WithPreferredDistance(RetreatDistance));
        }

        /// <summary>Шлях перекриває дерев'яна перегородка чи коробка — стріляємо в неї.</summary>
        /// <summary>
        /// Перешкода на шляху: руйнівну прострілюємо; рухомий ящик/кулю перестрибуємо, а якщо не виходить —
        /// виштовхуємо пострілом (куля штовхає об'єкт). Стріляємо лише тоді, коли немає пострілу по ворогу.
        /// </summary>
        private void HandleObstacleAhead(Vector2 myPosition, int horizontal, bool canShoot)
        {
            Vector2 direction = horizontal != 0 ? new Vector2(horizontal, 0f) : aimDirection;
            ObstacleKind kind = Destructibles.FindAhead(myPosition, direction, BreakableProbeDistance, out Vector2 point);
            if (kind == ObstacleKind.None)
            {
                return;
            }

            if (kind == ObstacleKind.Movable && horizontal != 0 && (point - myPosition).magnitude < MovableJumpDistance
                && (data.isGrounded || data.isWallGrab))
            {
                pendingJump = true; // впираємося в ящик — на нього/через нього
            }

            if (!canShoot)
            {
                return;
            }

            // Та сама перешкода — рахуємо постріли; після ліміту здаємося (може бути незламною).
            obstacleShots = (point - obstaclePoint).sqrMagnitude < 1f ? obstacleShots + 1 : 1;
            obstaclePoint = point;
            if (obstacleShots <= MaxShotsPerObstacle)
            {
                aimDirection = (point - myPosition).normalized;
                shooting = true;
            }
        }

        private const float StuckBeforeDeathSeconds = 2f;
        private const float RouteCheckInterval = 2f;

        /// <summary>Варіант A/B закріплений на весь матч; у наступному матчі боти міняються варіантами (ШІ-чат, крок 1).</summary>
        private bool IsVariantB => DeathCause.IsVariantB(data.player.playerID, DamageTracker.MatchIndex);

        /// <summary>Варіант B експерименту вимикає вибрану функцію.</summary>
        private bool IsDisabled(BotExperiment feature)
        {
            return Plugin.Experiment.Value == feature && IsVariantB;
        }

        private string Variant => Plugin.Experiment.Value == BotExperiment.None ? "-" : IsVariantB ? "B" : "A";

        /// <summary>Рядок RESULT при смерті — для підрахунку A/B (хто частіше гине).</summary>
        private void ReportDeath()
        {
            bool dead = data.dead;
            if (dead && !wasDead)
            {
                BotLog.Write(DescribeDeath());
            }

            wasDead = dead;
        }

        private string DescribeDeath()
        {
            int id = data.player.playerID;
            string cause = "unknown";
            string killer = "-";
            string weapon = "-";
            if (DamageTracker.TryGetLastHit(id, out DamageTracker.Hit hit))
            {
                cause = hit.Cause.ToString().ToLowerInvariant();
                killer = hit.SourceId >= 0 ? hit.SourceId.ToString() : "-";
                weapon = hit.Weapon;
            }

            Vector2 at = transform.position;
            bool stuck = Time.time - navigation.LastStuckAt <= StuckBeforeDeathSeconds;
            float life = Time.time - aliveSince;
            return System.FormattableString.Invariant(
                $"RESULT died bot={id} variant={Variant} experiment={Plugin.Experiment.Value} match={DamageTracker.MatchIndex} map={NavMap.CurrentMapName()} t={Time.time:F0} cause={cause} killer={killer} weapon={weapon} life={life:F1} stuck2s={stuck} pos=({at.x:F1},{at.y:F1})");
        }

        /// <summary>Чи бачив бот ціль на останньому рішенні (для запису F9).</summary>
        public bool LastCanSee { get; private set; }

        /// <summary>Чи мав бот чисту траєкторію пострілу на останньому рішенні (для запису F9).</summary>
        public bool LastClearShot { get; private set; }

        private Player FindTarget(Vector2 myPosition)
        {
            Player me = data.player;
            List<Player> enemies = PlayerManager.instance.players
                .Where(p => p != null && p != me && p.teamID != me.teamID && p.data != null && !p.data.dead)
                .OrderBy(p => ((Vector2)p.transform.position - myPosition).sqrMagnitude)
                .ToList();
            Player nearest = enemies.FirstOrDefault();
            if (nearest == null)
            {
                return null;
            }

            if (routeOverride != null && !enemies.Contains(routeOverride))
            {
                routeOverride = null;
            }

            if (Time.time >= nextRouteCheckAt)
            {
                nextRouteCheckAt = Time.time + RouteCheckInterval;
                UpdateRouteOverride(myPosition, nearest, enemies);
            }

            return routeOverride ?? nearest;
        }

        /// <summary>
        /// До найближчого ворога немає шляху понад 3 с і пострілу теж немає — беремо найближчого, до якого шлях є
        /// (бот унизу карти не топчеться під недосяжними виступами). Найближчий знову досяжний — повертаємося до нього.
        /// </summary>
        private void UpdateRouteOverride(Vector2 myPosition, Player nearest, List<Player> enemies)
        {
            if (aiming.HasClearShot || navigation.HasRouteTo(myPosition, nearest.transform.position))
            {
                noRouteSince = null;
                routeOverride = null;
                return;
            }

            noRouteSince = noRouteSince ?? Time.time;
            if (Time.time - noRouteSince.Value < Stalemate.NoRouteSeconds
                || (routeOverride != null && navigation.HasRouteTo(myPosition, routeOverride.transform.position)))
            {
                return;
            }

            routeOverride = enemies.Skip(1).FirstOrDefault(p => navigation.HasRouteTo(myPosition, p.transform.position));
        }

        private void UpdateBlock(Vector2 myPosition, BotSettings settings)
        {
            if (data.block == null || data.block.IsOnCD())
            {
                return;
            }

            float window = BlockReactionWindow + settings.ThinkInterval;
            foreach (ProjectileHit bullet in BulletCache.Get())
            {
                if (bullet == null || bullet.ownPlayer == null || bullet.ownPlayer.teamID == data.player.teamID)
                {
                    continue;
                }

                MoveTransform movement = bullet.GetComponent<MoveTransform>();
                if (movement == null)
                {
                    continue;
                }

                Vector2 relative = myPosition - (Vector2)bullet.transform.position;
                Vector2 velocity = movement.velocity;
                float radius = bullet.GetComponentInChildren<Homing>() != null ? HomingBulletHitRadius : BulletHitRadius;
                if (BotMath.IsIncomingThreat(relative.x, relative.y, velocity.x, velocity.y, radius, window))
                {
                    if (Random.value < settings.BlockChance)
                    {
                        pendingBlock = true;
                    }

                    return;
                }
            }
        }

        private void DisableVanillaAi()
        {
            foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && VanillaAiTypes.Contains(behaviour.GetType().Name))
                {
                    behaviour.enabled = false;
                }
            }

            PlayerAPI api = GetComponent<PlayerAPI>();
            if (api != null)
            {
                api.enabled = false;
            }
        }
    }
}
