using System;
using System.Collections.Generic;

namespace RoundsBots.Logic
{
    /// <summary>
    /// Чисельна симуляція кулі так само, як MoveTransform.Update у грі: гравітація, опір (drag, лише коли швидкість
    /// більша за 2 і за dragMinSpeed), швидкість симуляції. Потрібна, коли є drag — аналітична парабола тоді мажe.
    /// Ідея (врахувати drag) — з RoundsWithBots; формула — з коду гри.
    /// </summary>
    public static class BulletSim
    {
        public const float Step = 0.02f;
        private const float MaxDragStep = 0.02f;
        private const float DragSpeedFloor = 2f;

        /// <summary>Чи потрібна симуляція (є опір чи змінена швидкість симуляції).</summary>
        public static bool Needed(float drag, float simulationSpeed)
        {
            return drag > 0f || Math.Abs(simulationSpeed - 1f) > 0.001f;
        }

        /// <summary>Точки польоту (час, x, y) від старту до <paramref name="maxTime"/> с.</summary>
        public static List<(float t, float x, float y)> Path(
            float dirX, float dirY, float speed, float gravity, float drag, float dragMinSpeed, float simulationSpeed,
            float boostX, float boostY, float maxTime)
        {
            var points = new List<(float, float, float)>();
            float vx = dirX * speed + boostX;
            float vy = dirY * speed + boostY;
            float x = 0f, y = 0f;
            for (float t = Step; t <= maxTime + 1e-4f; t += Step)
            {
                float dt = Step * simulationSpeed;
                float dragStep = Math.Min(Step, MaxDragStep) * simulationSpeed;
                vy -= gravity * dt;
                float v = (float)Math.Sqrt(vx * vx + vy * vy);
                if (v > DragSpeedFloor && v > dragMinSpeed)
                {
                    float k = Math.Max(0f, Math.Min(1f, drag * dragStep));
                    vx -= vx * k;
                    vy -= vy * k;
                }

                x += vx * dt;
                y += vy * dt;
                points.Add((t, x, y));
            }

            return points;
        }

        /// <summary>Найближчий підхід траєкторії до точки (dx, dy): відстань і час.</summary>
        public static (float miss, float time) ClosestApproach(List<(float t, float x, float y)> path, float dx, float dy)
        {
            float best = float.MaxValue;
            float bestT = 0f;
            foreach ((float t, float x, float y) in path)
            {
                float d = (x - dx) * (x - dx) + (y - dy) * (y - dy);
                if (d < best)
                {
                    best = d;
                    bestT = t;
                }
            }

            return ((float)Math.Sqrt(best), bestT);
        }

        /// <summary>
        /// Кут пострілу з опором: перебір навколо <paramref name="baseAngleDeg"/> (±<paramref name="searchDeg"/>, крок 1°),
        /// поки куля не пройде ближче <paramref name="tolerance"/> од. від цілі.
        /// </summary>
        public static bool TryAim(
            float dx, float dy, float speed, float gravity, float drag, float dragMinSpeed, float simulationSpeed,
            float boostX, float boostY, float baseAngleDeg, float searchDeg, float maxTime, float tolerance,
            out float dirX, out float dirY, out float flightTime)
        {
            dirX = dirY = flightTime = 0f;
            float bestMiss = float.MaxValue;
            for (int i = 0; i <= searchDeg * 2; i++)
            {
                float offset = i % 2 == 0 ? i / 2 : -(i + 1) / 2;
                double rad = (baseAngleDeg + offset) * Math.PI / 180.0;
                float cx = (float)Math.Cos(rad);
                float cy = (float)Math.Sin(rad);
                var path = Path(cx, cy, speed, gravity, drag, dragMinSpeed, simulationSpeed, boostX, boostY, maxTime);
                (float miss, float time) = ClosestApproach(path, dx, dy);
                if (miss < bestMiss)
                {
                    bestMiss = miss;
                    dirX = cx;
                    dirY = cy;
                    flightTime = time;
                }

                if (bestMiss < tolerance)
                {
                    break;
                }
            }

            return bestMiss < tolerance;
        }
    }
}
