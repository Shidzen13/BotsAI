using RoundsBots.Logic;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Прицілювання з урахуванням реальної фізики кулі: швидкість і гравітація беруться зі зброї бота
    /// (з урахуванням карток), ціль упереджується за її швидкістю, а траєкторія перевіряється на перешкоди.
    /// </summary>
    internal sealed class BotAiming
    {
        private const float DefaultBulletGravity = 30f;
        private const float MinBulletSpeed = 5f;
        private const int TrajectorySamples = 14;
        private const float MaxFlightTime = 3f;
        private const float DragSearchDegrees = 45f;
        private const float DragAimTolerance = 0.5f;
        private const float BankSearchInterval = 0.25f;
        private const int BankSamples = 36;
        private const float BankMaxTime = 1f;
        private const float BankHitRadius = 0.9f;
        private static readonly RaycastHit2D[] BankHits = new RaycastHit2D[8];
        private static readonly SegmentCast WallCast = BankShot.WithArenaBounds(CastWall, BotMath.ArenaHalfWidth, BotMath.ArenaHalfHeight);

        private const float HomingLaunchDistance = 4f;   // перший відрізок, який куля летить «сама»
        private const float HomingMaxAngle = 65f;        // гра наводить кулю, якщо ціль у межах 70° від напрямку
        private static readonly float[] HomingAngles = { 0f, 25f, -25f, 50f, -50f };

        private float nextBankSearchAt;
        private Vector2? bankDirection;

        private readonly CharacterData data;
        private Player trackedTarget;
        private Vector2 trackedPosition;
        private float trackedTime;
        private Vector2 targetVelocity;
        private Vector2 bulletBoost;
        private float bulletDrag;
        private float bulletDragMinSpeed = 1f;
        private float bulletSimulationSpeed = 1f;

        private bool UsesDragSim => BulletSim.Needed(bulletDrag, bulletSimulationSpeed);

        public BotAiming(CharacterData data)
        {
            this.data = data;
        }

        /// <summary>Чи є чиста траєкторія з останнього розрахунку (можна стріляти).</summary>
        public bool HasClearShot { get; private set; }

        /// <summary>Остання використана швидкість і гравітація кулі (для діагностики в лозі).</summary>
        public float LastBulletSpeed { get; private set; }

        public float LastBulletGravity { get; private set; }

        /// <summary>Розраховує напрямок прицілу до цілі. Повертає нормалізований вектор.</summary>
        public Vector2 Aim(Vector2 from, Player target, float aimErrorDegrees)
        {
            UpdateTargetVelocity(target);
            GetBulletPhysics(out float speed, out float gravity);
            LastBulletSpeed = speed;
            LastBulletGravity = gravity;

            Vector2 targetPosition = target.data.playerVel.position;
            Vector2 aim = (targetPosition - from).normalized;
            HasClearShot = false;

            // Спершу настильна траєкторія, якщо закрита — навісна.
            foreach (bool highArc in new[] { false, true })
            {
                if (!TrySolve(from, targetPosition, speed, gravity, highArc, out Vector2 direction, out float flightTime))
                {
                    continue;
                }

                if (IsTrajectoryClear(from, direction, speed, gravity, flightTime))
                {
                    aim = direction;
                    HasClearShot = true;
                    break;
                }

                if (!highArc)
                {
                    aim = direction; // запасний варіант, якщо обидві закриті
                }
            }

            LastBankShot = false;
            LastHomingShot = false;
            if (!HasClearShot && HasHoming() && TryHomingShot(from, targetPosition, out Vector2 homing))
            {
                aim = homing;
                HasClearShot = true;
                LastHomingShot = true;
            }

            if (!HasClearShot && TryBankShot(from, targetPosition, speed, gravity, out Vector2 bank))
            {
                aim = bank;
                HasClearShot = true;
                LastBankShot = true;
            }

            Vector2 aimed = Rotate(aim, Random.Range(-aimErrorDegrees, aimErrorDegrees));
            if (HasClearShot && !LastBankShot && !LastHomingShot && !IsDirectionClear(from, aimed, targetPosition, speed, gravity))
            {
                return aim; // розкид завів би кулю в перегородку — стріляємо точно
            }

            return aimed;
        }

        /// <summary>Чи чиста траєкторія для вже повернутого (з розкидом) напрямку — на ту саму відстань до цілі.</summary>
        private bool IsDirectionClear(Vector2 from, Vector2 direction, Vector2 targetPosition, float speed, float gravity)
        {
            float flightTime = UsesDragSim
                ? BulletSim.ClosestApproach(SimPath(direction, speed, gravity), targetPosition.x - from.x, targetPosition.y - from.y).time
                : Mathf.Min(BotMath.FlightTime(targetPosition.x - from.x, direction.x, speed), MaxFlightTime);
            return IsTrajectoryClear(from, direction, speed, gravity, flightTime);
        }

        /// <summary>Чи останній постріл — самонавідний «у бік» цілі (для лога).</summary>
        public bool LastHomingShot { get; private set; }

        /// <summary>Картка Homing додає кулям компонент Homing (через Gun.objectsToSpawn → AddToProjectile).</summary>
        private bool HasHoming()
        {
            Gun gun = data.weaponHandler != null ? data.weaponHandler.gun : null;
            if (gun == null || gun.objectsToSpawn == null)
            {
                return false;
            }

            foreach (ObjectsToSpawn spawn in gun.objectsToSpawn)
            {
                if (spawn != null && spawn.AddToProjectile != null && spawn.AddToProjectile.GetComponentInChildren<Homing>(true) != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Самонавідна куля сама довертає до видимої цілі (і без гравітації). Досить, щоб перший відрізок
        /// був вільний, а з його кінця ціль була видна в межах кута наведення.
        /// </summary>
        private static bool TryHomingShot(Vector2 from, Vector2 target, out Vector2 direction)
        {
            direction = Vector2.zero;
            LayerMask mask = PlayerManager.instance.canSeePlayerMask;
            Vector2 toTarget = (target - from).normalized;
            foreach (float angle in HomingAngles)
            {
                Vector2 candidate = Rotate(toTarget, angle);
                Vector2 launch = from + candidate * HomingLaunchDistance;
                if (IsBlocked(from, launch, mask) || IsBlocked(launch, target, mask)
                    || Vector2.Angle(candidate, target - launch) > HomingMaxAngle)
                {
                    continue;
                }

                direction = candidate;
                return true;
            }

            return false;
        }

        /// <summary>Чи останній постріл — рикошетом (для лога).</summary>
        public bool LastBankShot { get; private set; }

        /// <summary>Картки з відскоками: шукаємо постріл через стіну/стелю, коли прямого немає.</summary>
        private bool TryBankShot(Vector2 from, Vector2 target, float speed, float gravity, out Vector2 direction)
        {
            direction = Vector2.zero;
            Gun gun = data.weaponHandler != null ? data.weaponHandler.gun : null;
            if (gun == null || gun.reflects <= 0)
            {
                bankDirection = null;
                return false;
            }

            if (Time.time >= nextBankSearchAt)
            {
                nextBankSearchAt = Time.time + BankSearchInterval;
                bankDirection = BankShot.TryFind(from.x, from.y, speed, gravity, bulletBoost.x, bulletBoost.y, gun.reflects,
                    target.x, target.y, BankHitRadius, BankMaxTime, BankSamples, WallCast, out float x, out float y)
                    ? new Vector2(x, y)
                    : (Vector2?)null;
            }

            direction = bankDirection ?? Vector2.zero;
            return bankDirection.HasValue;
        }

        private static bool CastWall(float ax, float ay, float bx, float by, out float hitX, out float hitY, out float normalX, out float normalY)
        {
            hitX = hitY = normalX = normalY = 0f;
            int count = Physics2D.LinecastNonAlloc(new Vector2(ax, ay), new Vector2(bx, by), BankHits, PlayerManager.instance.canSeePlayerMask);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = BankHits[i];
                if (hit.collider == null || hit.collider.isTrigger || hit.distance >= best)
                {
                    continue;
                }

                Transform root = hit.transform.root;
                if (root.GetComponent<Player>() != null || root.GetComponent<SpawnedAttack>() != null)
                {
                    continue;
                }

                best = hit.distance;
                hitX = hit.point.x;
                hitY = hit.point.y;
                normalX = hit.normal.x;
                normalY = hit.normal.y;
            }

            return best < float.PositiveInfinity;
        }

        private bool TrySolve(Vector2 from, Vector2 targetPosition, float speed, float gravity, bool highArc, out Vector2 direction, out float flightTime)
        {
            direction = Vector2.zero;
            flightTime = 0f;
            Vector2 predicted = targetPosition;

            // Кілька ітерацій: оцінюємо час польоту, зсуваємо ціль на її швидкість
            // і компенсуємо постійний поштовх кулі (worldForce), перераховуємо.
            for (int i = 0; i < 3; i++)
            {
                Vector2 delta = predicted - from;
                if (!BotMath.TryBallisticAim(delta.x, delta.y, speed, gravity, highArc, out float x, out float y))
                {
                    return false;
                }

                direction = new Vector2(x, y);
                flightTime = Mathf.Min(BotMath.FlightTime(delta.x, x, speed), MaxFlightTime);
                predicted = targetPosition + (targetVelocity - bulletBoost) * flightTime;
            }

            if (UsesDragSim)
            {
                // Опір повітря: парабола мажe — уточнюємо кут чисельною симуляцією навколо аналітичного.
                Vector2 delta = predicted - from;
                float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                if (!BulletSim.TryAim(delta.x, delta.y, speed, gravity, bulletDrag, bulletDragMinSpeed, bulletSimulationSpeed,
                        bulletBoost.x, bulletBoost.y, baseAngle, DragSearchDegrees, MaxFlightTime, DragAimTolerance,
                        out float sx, out float sy, out float simTime))
                {
                    return false;
                }

                direction = new Vector2(sx, sy);
                flightTime = simTime;
            }

            return true;
        }

        private bool IsTrajectoryClear(Vector2 from, Vector2 direction, float speed, float gravity, float flightTime)
        {
            // Куля вилітає з дула і має товщину: перевіряємо три паралельні траєкторії від дула (центр і ±радіус).
            Vector2 muzzle = from + direction.normalized * MuzzleOffset();
            Vector2 side = new Vector2(-direction.y, direction.x).normalized * BulletRadius;
            return IsSingleTrajectoryClear(muzzle, direction, speed, gravity, flightTime)
                && IsSingleTrajectoryClear(muzzle + side, direction, speed, gravity, flightTime)
                && IsSingleTrajectoryClear(muzzle - side, direction, speed, gravity, flightTime);
        }

        /// <summary>Приблизний радіус кулі (од.) — щоб не чіпляти краї перегородок.</summary>
        private const float BulletRadius = 0.3f;

        private const float MaxMuzzleOffset = 2f;

        /// <summary>Відстань від центру гравця до точки вильоту кулі (дуло).</summary>
        private float MuzzleOffset()
        {
            Gun gun = data.weaponHandler != null ? data.weaponHandler.gun : null;
            if (gun == null || gun.shootPosition == null)
            {
                return 0f;
            }

            return Mathf.Min(Vector2.Distance(gun.shootPosition.position, data.transform.position), MaxMuzzleOffset);
        }

        private bool IsSingleTrajectoryClear(Vector2 from, Vector2 direction, float speed, float gravity, float flightTime)
        {
            LayerMask mask = PlayerManager.instance.canSeePlayerMask;
            float drill = DrillMeters();
            float walls = 0f;
            var path = UsesDragSim ? SimPath(direction, speed, gravity) : null;
            Vector2 previous = from;
            for (int i = 1; i <= TrajectorySamples; i++)
            {
                float t = flightTime * i / TrajectorySamples;
                Vector2 point = from + Offset(path, direction, speed, gravity, t);
                if (IsBlocked(previous, point, mask))
                {
                    // Drill Ammo: куля пробиває стіни — рахуємо їх сумарну товщину.
                    walls += drill > 0f ? WallThickness(previous, point, mask) : float.PositiveInfinity;
                    if (!BotMath.DrillPierces(walls, drill))
                    {
                        return false;
                    }
                }

                previous = point;
            }

            return true;
        }

        private System.Collections.Generic.List<(float t, float x, float y)> SimPath(Vector2 direction, float speed, float gravity)
        {
            return BulletSim.Path(direction.x, direction.y, speed, gravity, bulletDrag, bulletDragMinSpeed, bulletSimulationSpeed,
                bulletBoost.x, bulletBoost.y, MaxFlightTime);
        }

        /// <summary>Зсув кулі від точки пострілу через t с: з симуляції (якщо є drag) або параболою.</summary>
        private Vector2 Offset(System.Collections.Generic.List<(float t, float x, float y)> path, Vector2 direction, float speed, float gravity, float t)
        {
            if (path != null && path.Count > 0)
            {
                int index = Mathf.Clamp(Mathf.RoundToInt(t / BulletSim.Step) - 1, 0, path.Count - 1);
                return new Vector2(path[index].x, path[index].y);
            }

            (float x, float y) = BotMath.BulletPosition(direction.x, direction.y, speed, gravity, t);
            return new Vector2(x, y) + bulletBoost * t;
        }

        private int drillCacheCount = -1;
        private float drillCacheMeters;

        /// <summary>Скільки метрів стіни пробиває куля (сума RayHitDrill усіх карток Drill Ammo); 0 — без дриля.</summary>
        private float DrillMeters()
        {
            Gun gun = data.weaponHandler != null ? data.weaponHandler.gun : null;
            if (gun == null || gun.objectsToSpawn == null)
            {
                return 0f;
            }

            if (gun.objectsToSpawn.Length != drillCacheCount)
            {
                drillCacheCount = gun.objectsToSpawn.Length;
                drillCacheMeters = 0f;
                foreach (ObjectsToSpawn spawn in gun.objectsToSpawn)
                {
                    if (spawn?.AddToProjectile == null)
                    {
                        continue;
                    }

                    foreach (RayHitDrill drill in spawn.AddToProjectile.GetComponentsInChildren<RayHitDrill>(true))
                    {
                        drillCacheMeters += drill.metersOfDrilling;
                    }
                }
            }

            return drillCacheMeters;
        }

        /// <summary>Товщина стіни на відрізку: від першого входу з боку a до першого входу з боку b.</summary>
        private static float WallThickness(Vector2 a, Vector2 b, LayerMask mask)
        {
            float length = Vector2.Distance(a, b);
            float enter = FirstSolidDistance(a, b, mask);
            float exit = FirstSolidDistance(b, a, mask);
            return Mathf.Clamp(length - (float.IsInfinity(enter) ? 0f : enter) - (float.IsInfinity(exit) ? 0f : exit), 0f, length);
        }

        private static float FirstSolidDistance(Vector2 a, Vector2 b, LayerMask mask)
        {
            float best = float.PositiveInfinity;
            foreach (RaycastHit2D hit in Physics2D.LinecastAll(a, b, mask))
            {
                if (IsSolidHit(hit) && hit.distance < best)
                {
                    best = hit.distance;
                }
            }

            return best;
        }

        private static bool IsSolidHit(RaycastHit2D hit)
        {
            if (hit.collider == null || hit.collider.isTrigger)
            {
                return false;
            }

            Transform root = hit.transform != null ? hit.transform.root : null;
            return root != null && root.GetComponent<Player>() == null && root.GetComponent<SpawnedAttack>() == null
                && !Destructibles.IsDestructible(hit.collider);
        }

        private static bool IsBlocked(Vector2 a, Vector2 b, LayerMask mask)
        {
            // Та сама логіка, що й у PlayerManager.CanSeePlayer: гравці та атаки не вважаються перешкодою.
            // Руйнівні перегородки/коробки теж ні — куля їх ламає, і наступна пролетить.
            foreach (RaycastHit2D hit in Physics2D.LinecastAll(a, b, mask))
            {
                if (hit.collider != null && hit.collider.isTrigger)
                {
                    continue; // зони (космос, вода WillsWackyMapObjects) — кулі крізь них летять
                }

                Transform root = hit.transform != null ? hit.transform.root : null;
                if (root != null && root.GetComponent<Player>() == null && root.GetComponent<SpawnedAttack>() == null
                    && !Destructibles.IsDestructible(hit.collider))
                {
                    return true;
                }
            }

            return false;
        }

        private void GetBulletPhysics(out float speed, out float gravity)
        {
            speed = 0f;
            gravity = DefaultBulletGravity;
            bulletBoost = Vector2.zero;

            Gun gun = data.weaponHandler != null ? data.weaponHandler.gun : null;
            MoveTransform bullet = gun != null && gun.projectiles != null && gun.projectiles.Length > 0 && gun.projectiles[0].objectToSpawn != null
                ? gun.projectiles[0].objectToSpawn.GetComponent<MoveTransform>()
                : null;

            if (gun != null && bullet != null)
            {
                // Так само, як Gun при пострілі: localForce *= projectileSpeed, gravity *= gun.gravity.
                speed = bullet.localForce.magnitude * gun.projectileSpeed;
                gravity = bullet.gravity * gun.gravity;
                // Виміряно в грі: до швидкості кулі додається worldForce (зазвичай (0, 5)), теж множиться на gun.gravity.
                bulletBoost = (Vector2)bullet.worldForce * gun.gravity;
                // Як Gun.FireBurst: drag кулі = gun.drag (0..45), dragMinSpeed, simulationSpeed *= projectielSimulatonSpeed.
                bulletDrag = Mathf.Clamp(gun.drag, 0f, 45f);
                bulletDragMinSpeed = gun.dragMinSpeed;
                bulletSimulationSpeed = gun.projectielSimulatonSpeed; // у префаба simulationSpeed = 1
            }
            else
            {
                bulletDrag = 0f;
                bulletDragMinSpeed = 1f;
                bulletSimulationSpeed = 1f;
            }

            if (speed < MinBulletSpeed)
            {
                speed = MinBulletSpeed;
            }
        }

        private void UpdateTargetVelocity(Player target)
        {
            Vector2 position = target.data.playerVel.position;
            float now = Time.time;
            if (target != trackedTarget)
            {
                trackedTarget = target;
                targetVelocity = Vector2.zero;
            }
            else
            {
                float elapsed = now - trackedTime;
                if (elapsed > 0.01f && elapsed < 1f)
                {
                    Vector2 measured = (position - trackedPosition) / elapsed;
                    targetVelocity = Vector2.Lerp(targetVelocity, measured, 0.5f); // згладжуємо ривки
                }
            }

            trackedPosition = position;
            trackedTime = now;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos).normalized;
        }
    }
}
