using System;

namespace RoundsBots.Logic
{
    /// <summary>
    /// Звідки картка: ваніль чи мод. UnboundLib дає модовим карткам ім'я об'єкта «__ІНІЦІАЛИ_МОДА__Назва»
    /// (наприклад «__KEYS__Alchemist»); ванільні — без префікса. Копії на столі мають ще суфікс «(Clone)».
    /// </summary>
    public readonly struct CardOrigin
    {
        public const string VanillaName = "Vanilla";

        private CardOrigin(string mod)
        {
            Mod = mod;
        }

        /// <summary>Ініціали мода (KEYS, RSC, CM…) або «Vanilla».</summary>
        public string Mod { get; }

        public bool IsModded => Mod != VanillaName;

        public static CardOrigin Parse(string objectName)
        {
            const string Marker = "__";
            if (string.IsNullOrEmpty(objectName) || !objectName.StartsWith(Marker, StringComparison.Ordinal))
            {
                return new CardOrigin(VanillaName);
            }

            int end = objectName.IndexOf(Marker, Marker.Length, StringComparison.Ordinal);
            return end > Marker.Length
                ? new CardOrigin(objectName.Substring(Marker.Length, end - Marker.Length))
                : new CardOrigin(VanillaName);
        }

        public override string ToString() => Mod;
    }
}
