using System.Collections.Generic;

namespace RoundsBots.Logic
{
    /// <summary>Вибір вільного місця і кольору для бота в онлайн-кімнаті (RWF). Без Unity — тестується окремо.</summary>
    public static class LobbySlots
    {
        /// <summary>Перший вільний індекс (null у масиві), або -1, якщо місць немає.</summary>
        public static int FirstFreeSlot(IReadOnlyList<bool> occupied)
        {
            for (int i = 0; i < occupied.Count; i++)
            {
                if (!occupied[i])
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Перший колір, якого ще ніхто не має (кожен колір — окрема команда); якщо всі зайняті — 0.</summary>
        public static int FirstFreeColor(IEnumerable<int> usedColors, int colorCount)
        {
            var used = new HashSet<int>(usedColors);
            for (int color = 0; color < colorCount; color++)
            {
                if (!used.Contains(color))
                {
                    return color;
                }
            }

            return 0;
        }
    }
}
