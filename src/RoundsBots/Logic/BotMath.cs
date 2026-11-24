using System;

namespace RoundsBots.Logic
{
    /// <summary>Чиста математика для рішень бота (без Unity), щоб її можна було покрити тестами.</summary>
    public static class BotMath
    {
        private const float MaxAimLift = 6f;
        private const float AimLiftPerDistanceSquared = 0.02f;
        private const float DistanceTolerance = 5f; // широка «зона спокою»: людина більшість часу стоїть і стріляє

        /// <summary>
        /// Напрямок прицілу до цілі з поправкою на падіння кулі (ціль трохи вище) та похибкою.
        /// <paramref name="errorDegrees"/> — кут відхилення (може бути від'ємним).
        /// </summary>
        public static (float x, float y) Aim(float dx, float dy, float errorDegrees)
        {
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            float lift = Math.Min(MaxAimLift, distance * distance * AimLiftPerDistanceSquared);
            float ax = dx;
            float ay = dy + lift;

            double radians = errorDegrees * Math.PI / 180.0;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            float rx = ax * cos - ay * sin;
            float ry = ax * sin + ay * cos;

            float length = (float)Math.Sqrt(rx * rx + ry * ry);
            return length < 0.0001f ? (1f, 0f) : (rx / length, ry / length);
        }

        /// <summary>
        /// Балістичний приціл: під яким напрямком випустити кулю зі швидкістю <paramref name="speed"/>
        /// при гравітації <paramref name="gravity"/>, щоб влучити в точку (dx, dy).
        /// <paramref name="highArc"/> = true — навісна траєкторія (через перешкоди).
        /// Повертає false, якщо ціль поза досяжністю.
        /// </summary>
        public static bool TryBallisticAim(float dx, float dy, float speed, float gravity, bool highArc, out float dirX, out float dirY)
        {
            dirX = 0f;
            dirY = 0f;
            if (speed <= 0.01f)
            {
                return false;
            }

            if (gravity <= 0.0001f || Math.Abs(dx) < 0.001f)
            {
                float length = (float)Math.Sqrt(dx * dx + dy * dy);
                if (length < 0.0001f)
                {
                    return false;
                }

                dirX = dx / length;
                dirY = dy / length;
                return true;
            }

            double v2 = (double)speed * speed;
            double x = Math.Abs(dx);
            double discriminant = v2 * v2 - gravity * (gravity * x * x + 2.0 * dy * v2);
            if (discriminant < 0)
            {
                return false;
            }

            double root = Math.Sqrt(discriminant);
            double tan = (v2 + (highArc ? root : -root)) / (gravity * x);
            double angle = Math.Atan(tan);
            dirX = (float)(Math.Cos(angle) * Math.Sign(dx));
            dirY = (float)Math.Sin(angle);
            return true;
        }

        /// <summary>Межі арени з OutOfBoundsHandler: за ними гравця відкидає назад (без блоку — ще й 51 шкоди).</summary>
        public const float ArenaHalfWidth = 35.56f;
        public const float ArenaHalfHeight = 20f;

        /// <summary>Приблизна висота звичайного стрибка (з утриманням), од.</summary>
        public const float JumpApexEstimate = 6f;

        /// <summary>Приблизна висота короткого стрибка (натиснув і відпустив), од.</summary>
        public const float ShortJumpApexEstimate = 3f;

        /// <summary>Запас до верхньої межі арени: вище — гравця відкидає назад і б'є (51 шкоди).</summary>
        public const float CeilingMargin = 1.5f;

        /// <summary>Чи вилетить стрибок з висоти <paramref name="y"/> за верхню межу арени.</summary>
        public static bool JumpWouldHitCeiling(float y, float apex = JumpApexEstimate, float margin = CeilingMargin)
        {
            return y + apex >= ArenaHalfHeight - margin;
        }

        /// <summary>
        /// Чи пора відпустити стрибок у повітрі: летимо вгору, і за поточної швидкості <paramref name="vy"/> й гравітації
        /// <paramref name="gravity"/> верхня точка буде за межею арени.
        /// </summary>
        public static bool ShouldReleaseJump(float y, float vy, float gravity, float margin = CeilingMargin)
        {
            if (vy <= 0f || gravity <= 0f)
            {
                return false;
            }

            return y + vy * vy / (2f * gravity) >= ArenaHalfHeight - margin;
        }

        /// <summary>
        /// Відскок витрачено: після нього піднялися вище <paramref name="bouncedBelowY"/> і вже падаємо на
        /// <paramref name="drop"/> од. від верхньої точки.
        /// </summary>
        public static bool BounceSpent(float apexY, float y, float bouncedBelowY, float drop)
        {
            return apexY > bouncedBelowY + drop && y < apexY - drop;
        }

        /// <summary>Запас: дриль сповільнюється в стіні, тож розраховуємо лише на цю частку його метрів.</summary>
        public const float DrillSafety = 0.8f;

        /// <summary>Чи пробʼє куля з дрилем (Drill Ammo, <paramref name="drillMeters"/> м) стіни сумарної товщини.</summary>
        public static bool DrillPierces(float wallThickness, float drillMeters)
        {
            return wallThickness <= 0f || (drillMeters > 0f && wallThickness <= drillMeters * DrillSafety);
        }

        /// <summary>
        /// Падіння до відскоку: не тиснемо в стіну, щоб не зачепитися за неї (з висіння бот відстрибував і
        /// так і не долітав до низу — карта «торт», лівий край). Повертає горизонтальний ввід.
        /// </summary>
        public static int BounceFallHorizontal(int desired, bool bounced, bool wallAhead)
        {
            return !bounced && wallAhead ? 0 : desired;
        }

        /// <summary>
        /// Чи вилетить гравець за межу арени протягом <paramref name="lookAhead"/> секунд.
        /// Якщо так — час тиснути блок: тоді відскок сильніший і без шкоди.
        /// </summary>
        public static bool WillHitArenaBounds(float x, float y, float vx, float vy, float lookAhead)
        {
            float px = x + vx * lookAhead;
            float py = y + vy * lookAhead;
            return px <= -ArenaHalfWidth || px >= ArenaHalfWidth || py <= -ArenaHalfHeight || py >= ArenaHalfHeight;
        }

        /// <summary>Час польоту кулі до горизонтальної відстані dx при заданому напрямку.</summary>
        public static float FlightTime(float dx, float dirX, float speed)
        {
            float horizontalSpeed = Math.Abs(dirX * speed);
            return horizontalSpeed < 0.01f ? 0f : Math.Abs(dx) / horizontalSpeed;
        }

        /// <summary>Позиція кулі через час t (відносно точки пострілу).</summary>
        public static (float x, float y) BulletPosition(float dirX, float dirY, float speed, float gravity, float t)
        {
            return (dirX * speed * t, dirY * speed * t - 0.5f * gravity * t * t);
        }

        /// <summary>
        /// Горизонтальний рух: -1 (вліво), 0 (стояти), 1 (вправо).
        /// Якщо ціль не видно — йдемо до неї. Якщо видно — тримаємо бажану дистанцію.
        /// </summary>
        public static int Horizontal(float dx, float distance, float preferredDistance, bool canSeeTarget)
        {
            int towards = dx > 0f ? 1 : dx < 0f ? -1 : 0;
            if (!canSeeTarget)
            {
                return towards;
            }

            if (distance > preferredDistance + DistanceTolerance)
            {
                return towards;
            }

            if (distance < preferredDistance - DistanceTolerance)
            {
                return -towards;
            }

            return 0;
        }

        /// <summary>
        /// Чи летить куля в бота: шукаємо найближчу точку траєкторії в межах часового вікна.
        /// (relX, relY) — позиція бота відносно кулі, (velX, velY) — швидкість кулі.
        /// </summary>
        public static bool IsIncomingThreat(float relX, float relY, float velX, float velY, float hitRadius, float timeWindow)
        {
            float speedSquared = velX * velX + velY * velY;
            if (speedSquared < 0.0001f)
            {
                return relX * relX + relY * relY <= hitRadius * hitRadius;
            }

            float t = (relX * velX + relY * velY) / speedSquared;
            if (t < 0f || t > timeWindow)
            {
                return false;
            }

            float closestX = relX - velX * t;
            float closestY = relY - velY * t;
            return closestX * closestX + closestY * closestY <= hitRadius * hitRadius;
        }

        public const float CloseRangeDistance = 5f;
        private const float MinPreferredDistance = 4f;
        private const float RangeUsage = 0.7f;

        /// <summary>
        /// Бажана дистанція з урахуванням збірки: карти на блок (ПКМ: ударна хвиля, статичне поле тощо)
        /// і коротка дальність кулі тягнуть бота впритул, дальня зброя — тримати дистанцію.
        /// <paramref name="bulletRange"/> ≤ 0 — дальність не обмежена.
        /// </summary>
        public static float PreferredDistance(float baseDistance, float bulletRange, int closeRangeEffects)
        {
            float distance = baseDistance;
            if (closeRangeEffects > 0)
            {
                distance = Math.Min(distance, CloseRangeDistance);
            }

            if (bulletRange > 0f)
            {
                distance = Math.Min(distance, bulletRange * RangeUsage);
            }

            return Math.Max(MinPreferredDistance, distance);
        }

        /// <summary>Штрафуємо перехід лише за справжнє падіння зі стрибка чи зіскоку (таймаут і ходьба — тільки в лог).</summary>
        public static bool ShouldPenalise(NavMove move, bool fell)
        {
            return fell && (move == NavMove.Jump || move == NavMove.Fall);
        }

        /// <summary>Скільки секунд після відриву бот довіряє своєму стрибку через прірву.</summary>
        public const float GapJumpTrustSeconds = 1.5f;

        /// <summary>
        /// Над порожнечею під час стрибка через прірву: чи не розвертати бота до найближчої опори.
        /// Одразу після відриву найближча опора — край, з якого стрибнули, тож «рятувальник» повертав бота назад
        /// (гра з другом 2026-11-23: «намагаються перестрибнути, але бояться і повертаються»).
        /// Повертає: null — звичайний порятунок до найближчої опори; 0 — не втручатися (веде узятий крок маршруту);
        /// ±1 — летіти далі в бік стрибка.
        /// </summary>
        public static int? GapJumpEscape(bool committedRouteJump, int gapJumpDirection, float sinceGapJump, bool landingAhead)
        {
            if (committedRouteJump)
            {
                return 0;
            }

            if (gapJumpDirection != 0 && sinceGapJump >= 0f && sinceGapJump < GapJumpTrustSeconds && landingAhead)
            {
                return gapJumpDirection;
            }

            return null;
        }
    }
}
