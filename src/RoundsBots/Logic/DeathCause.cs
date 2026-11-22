using System;

namespace RoundsBots.Logic
{
    /// <summary>Від чого востаннє отримав шкоду гравець (для рядка RESULT died).</summary>
    public enum DamageCause
    {
        Unknown,
        Player,   // кулі, отрута, вибухи іншого гравця
        Self,     // власні кулі / самошкода карток
        Bounds,   // удар об межу арени без блоку (51 шкоди)
        Hazard,   // пилки, шипи, DamageBox без власника
    }

    public static class DeathCause
    {
        /// <summary>Наскільки близько до межі арени (x ±35.56, y ±20) — шкода без джерела вважається ударом об межу.</summary>
        public const float BoundsMargin = 2f;

        public static DamageCause Classify(int sourcePlayerId, int victimPlayerId, float victimX, float victimY)
        {
            if (sourcePlayerId >= 0)
            {
                return sourcePlayerId == victimPlayerId ? DamageCause.Self : DamageCause.Player;
            }

            bool nearBounds = Math.Abs(victimX) >= BotMath.ArenaHalfWidth - BoundsMargin
                || Math.Abs(victimY) >= BotMath.ArenaHalfHeight - BoundsMargin;
            return nearBounds ? DamageCause.Bounds : DamageCause.Hazard;
        }

        /// <summary>Варіант A/B на весь матч: у наступному матчі боти міняються варіантами (і стартовими місцями).</summary>
        public static bool IsVariantB(int playerId, int matchIndex) => (playerId + matchIndex) % 2 == 1;
    }
}
