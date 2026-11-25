using System.Collections.Generic;
using System.Linq;

namespace RoundsBots.Logic
{
    /// <summary>
    /// Пам'ять про невдалі переходи маршруту на одній карті (ШІ-чат 2026-11-24, 0.5.4).
    /// Раніше кожна невдача навіки штрафувала 81 перехід, і бот «боявся» прірви до кінця гри.
    /// Тепер: штраф лише за справжнє падіння, сам перехід +<see cref="FailPenalty"/>, сусіди по x±1 на тій самій висоті
    /// +<see cref="NeighbourPenalty"/>, стеля <see cref="MaxPenalty"/>; з кожним очком штраф ×<see cref="DecayFactor"/>;
    /// вдалий перехід знімає свій штраф повністю.
    /// </summary>
    public sealed class EdgeLearning
    {
        public const float FailPenalty = 8f;
        public const float NeighbourPenalty = 4f;
        /// <summary>Не долетіли вчасно — пів штрафу лише самому переходу: двічі поспіль = як падіння
        /// (гра 2026-11-25: без цього бот безкінечно пробував недосяжний стрибок на 5 угору і ходив по колу).</summary>
        public const float TimeoutPenalty = 4f;
        public const float TimeoutNeighbourPenalty = 2f;
        public const int TimeoutNeighbourReach = 2;
        public const float MaxPenalty = 24f;
        public const float DecayFactor = 0.5f;
        public const float ForgetBelow = 0.5f;

        private readonly Dictionary<(int fx, int fy, int tx, int ty), float> penalties = new Dictionary<(int, int, int, int), float>();

        public int Count => penalties.Count;

        public float PenaltyOf(int fx, int fy, int tx, int ty)
        {
            return penalties.TryGetValue((fx, fy, tx, ty), out float value) ? value : 0f;
        }

        /// <summary>Впали на переході from→to. Повертає змінені переходи (щоб оновити поточну сітку).</summary>
        public IReadOnlyList<(int fx, int fy, int tx, int ty)> RecordFailure(int fx, int fy, int tx, int ty)
        {
            var changed = new List<(int, int, int, int)>();
            Add((fx, fy, tx, ty), FailPenalty, changed);
            Add((fx - 1, fy, tx - 1, ty), NeighbourPenalty, changed);
            Add((fx + 1, fy, tx + 1, ty), NeighbourPenalty, changed);
            return changed;
        }

        /// <summary>Стрибок не вдався за час (без падіння) — <see cref="TimeoutPenalty"/> лише цьому переходу.</summary>
        public IReadOnlyList<(int fx, int fy, int tx, int ty)> RecordTimeout(int fx, int fy, int tx, int ty)
        {
            var changed = new List<(int, int, int, int)>();
            Add((fx, fy, tx, ty), TimeoutPenalty, changed);
            // Сусідні цілі в тому самому ряду (гра 2026-11-25: бот перебирав той самий недосяжний уступ з dx=-4,-3,-2).
            for (int d = -TimeoutNeighbourReach; d <= TimeoutNeighbourReach; d++)
            {
                if (d != 0)
                {
                    Add((fx, fy, tx + d, ty), TimeoutNeighbourPenalty, changed);
                }
            }

            return changed;
        }

        /// <summary>Перехід непрохідний (коробку не пробили) — одразу <see cref="MaxPenalty"/>, сусідів не чіпаємо.</summary>
        public bool RecordBlocked(int fx, int fy, int tx, int ty)
        {
            var changed = new List<(int, int, int, int)>();
            Add((fx, fy, tx, ty), MaxPenalty, changed);
            return changed.Count > 0;
        }

        /// <summary>Перехід удався — штраф знімаємо повністю (сусідів не чіпаємо).</summary>
        public bool RecordSuccess(int fx, int fy, int tx, int ty)
        {
            return penalties.Remove((fx, fy, tx, ty));
        }

        /// <summary>Нове очко: усі штрафи ×<see cref="DecayFactor"/>, дрібні забуваємо.</summary>
        public void Decay()
        {
            foreach (var key in penalties.Keys.ToList())
            {
                float value = penalties[key] * DecayFactor;
                if (value < ForgetBelow)
                {
                    penalties.Remove(key);
                }
                else
                {
                    penalties[key] = value;
                }
            }
        }

        public IEnumerable<KeyValuePair<(int fx, int fy, int tx, int ty), float>> All => penalties;

        private void Add((int, int, int, int) edge, float amount, List<(int, int, int, int)> changed)
        {
            float old = penalties.TryGetValue(edge, out float value) ? value : 0f;
            float next = System.Math.Min(MaxPenalty, old + amount);
            if (next != old)
            {
                penalties[edge] = next;
                changed.Add(edge);
            }
        }
    }
}
