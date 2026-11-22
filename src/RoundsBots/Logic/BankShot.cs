using System;

namespace RoundsBots.Logic
{
    /// <summary>
    /// Перевірка відрізка на перешкоду: повертає true і точку та нормаль удару, якщо відрізок a→b у щось влучає.
    /// У грі — Physics2D.Linecast, у тестах — проста геометрія.
    /// </summary>
    public delegate bool SegmentCast(float ax, float ay, float bx, float by, out float hitX, out float hitY, out float normalX, out float normalY);

    /// <summary>
    /// Постріл з рикошетом (картки з відскоками кулі): симулює кулю з гравітацією, відбиваючи від стін,
    /// і шукає напрямок, за яким вона долетить до цілі, коли прямого пострілу немає. Без Unity — тестується окремо.
    /// </summary>
    public static class BankShot
    {
        public const float StepSeconds = 0.03f;
        private const float SurfaceOffset = 0.05f;

        /// <summary>Летить куля і чи влучає в коло (tx, ty, radius) до <paramref name="maxTime"/>, маючи <paramref name="bounces"/> відскоків.</summary>
        public static bool Simulate(
            float sx, float sy, float dirX, float dirY, float speed, float gravity, float boostX, float boostY,
            int bounces, float tx, float ty, float radius, float maxTime, SegmentCast cast, out float hitTime)
        {
            hitTime = 0f;
            float x = sx;
            float y = sy;
            float vx = dirX * speed + boostX;
            float vy = dirY * speed + boostY;
            bool bounced = false;

            for (float t = 0f; t < maxTime; t += StepSeconds)
            {
                vy -= gravity * StepSeconds;
                float nx = x + vx * StepSeconds;
                float ny = y + vy * StepSeconds;

                if (DistanceToSegment(tx, ty, x, y, nx, ny) <= radius)
                {
                    hitTime = t;
                    return bounced; // прямі постріли рахує звичайний приціл — тут лише рикошети
                }

                if (cast(x, y, nx, ny, out float hx, out float hy, out float normalX, out float normalY))
                {
                    if (bounces <= 0)
                    {
                        return false;
                    }

                    bounces--;
                    bounced = true;
                    float dot = vx * normalX + vy * normalY;
                    vx -= 2f * dot * normalX;
                    vy -= 2f * dot * normalY;
                    nx = hx + normalX * SurfaceOffset;
                    ny = hy + normalY * SurfaceOffset;
                }

                x = nx;
                y = ny;
            }

            return false;
        }

        /// <summary>Перебирає <paramref name="samples"/> напрямків по колу і повертає той, що влучає найшвидше.</summary>
        public static bool TryFind(
            float sx, float sy, float speed, float gravity, float boostX, float boostY, int bounces,
            float tx, float ty, float radius, float maxTime, int samples, SegmentCast cast, out float dirX, out float dirY)
        {
            dirX = 0f;
            dirY = 0f;
            float bestTime = float.PositiveInfinity;
            for (int i = 0; i < samples; i++)
            {
                double angle = 2.0 * Math.PI * i / samples;
                float cx = (float)Math.Cos(angle);
                float cy = (float)Math.Sin(angle);
                if (Simulate(sx, sy, cx, cy, speed, gravity, boostX, boostY, bounces, tx, ty, radius, maxTime, cast, out float time)
                    && time < bestTime)
                {
                    bestTime = time;
                    dirX = cx;
                    dirY = cy;
                }
            }

            return bestTime < float.PositiveInfinity;
        }

        /// <summary>
        /// Додає межі арени як стіни: у грі куля з відскоками відбивається й від країв екрана (ScreenEdgeBounce).
        /// Повертає ближчий удар — об стіну карти чи об межу.
        /// </summary>
        public static SegmentCast WithArenaBounds(SegmentCast inner, float halfWidth, float halfHeight)
        {
            return (float ax, float ay, float bx, float by, out float hx, out float hy, out float nx, out float ny) =>
            {
                bool hitWall = inner(ax, ay, bx, by, out hx, out hy, out nx, out ny);
                float wallDistance = hitWall ? (hx - ax) * (hx - ax) + (hy - ay) * (hy - ay) : float.PositiveInfinity;

                if (TryCrossBound(ax, ay, bx, by, halfWidth, halfHeight, out float ex, out float ey, out float enx, out float eny))
                {
                    float edgeDistance = (ex - ax) * (ex - ax) + (ey - ay) * (ey - ay);
                    if (edgeDistance < wallDistance)
                    {
                        hx = ex;
                        hy = ey;
                        nx = enx;
                        ny = eny;
                        return true;
                    }
                }

                return hitWall;
            };
        }

        private static bool TryCrossBound(float ax, float ay, float bx, float by, float hw, float hh,
            out float hx, out float hy, out float nx, out float ny)
        {
            hx = hy = nx = ny = 0f;
            float best = float.PositiveInfinity;
            float bestNx = 0f;
            float bestNy = 0f;
            var crossings = new[]
            {
                (cross: bx > hw && ax <= hw, t: (hw - ax) / (bx - ax), nx: -1f, ny: 0f),
                (cross: bx < -hw && ax >= -hw, t: (-hw - ax) / (bx - ax), nx: 1f, ny: 0f),
                (cross: by > hh && ay <= hh, t: (hh - ay) / (by - ay), nx: 0f, ny: -1f),
                (cross: by < -hh && ay >= -hh, t: (-hh - ay) / (by - ay), nx: 0f, ny: 1f),
            };
            foreach (var c in crossings)
            {
                if (c.cross && c.t >= 0f && c.t <= 1f && c.t < best)
                {
                    best = c.t;
                    bestNx = c.nx;
                    bestNy = c.ny;
                }
            }

            if (best == float.PositiveInfinity)
            {
                return false;
            }

            nx = bestNx;
            ny = bestNy;
            hx = ax + best * (bx - ax);
            hy = ay + best * (by - ay);
            return true;
        }

        private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float lengthSquared = dx * dx + dy * dy;
            float t = lengthSquared > 0f ? Math.Max(0f, Math.Min(1f, ((px - ax) * dx + (py - ay) * dy) / lengthSquared)) : 0f;
            float cx = ax + t * dx - px;
            float cy = ay + t * dy - py;
            return (float)Math.Sqrt(cx * cx + cy * cy);
        }
    }
}
