using System.Collections.Generic;
using System.Linq;

namespace RoundsBots.Logic
{
    /// <summary>
    /// Кружляння на місці: усі позиції за останні <see cref="WindowSeconds"/> с лежать у колі радіуса
    /// <see cref="Radius"/>. Лише для діагностики (ROUTE_LOOP у лозі).
    /// </summary>
    public sealed class LoopDetector
    {
        public const float WindowSeconds = 10f;
        public const float Radius = 4f;

        private readonly Queue<(float t, float x, float y)> samples = new Queue<(float, float, float)>();

        public void Add(float time, float x, float y)
        {
            samples.Enqueue((time, x, y));
            while (samples.Count > 0 && time - samples.Peek().t > WindowSeconds)
            {
                samples.Dequeue();
            }
        }

        public void Clear() => samples.Clear();

        /// <summary>Чи кружляємо: вікно заповнене (≥ 90 %) і всі точки близько до центру.</summary>
        public bool IsLooping(float now)
        {
            if (samples.Count < 3 || now - samples.Peek().t < WindowSeconds * 0.9f)
            {
                return false;
            }

            float cx = samples.Average(s => s.x);
            float cy = samples.Average(s => s.y);
            return samples.All(s => (s.x - cx) * (s.x - cx) + (s.y - cy) * (s.y - cy) <= Radius * Radius);
        }
    }
}
