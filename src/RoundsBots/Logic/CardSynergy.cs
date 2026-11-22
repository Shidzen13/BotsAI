using System;
using System.Collections.Generic;
using System.Linq;

namespace RoundsBots.Logic
{
    /// <summary>
    /// Синергії карток: бонус картці, яка утворює комбо з тим, що бот уже має (оцінка кожної картки окремо цього не бачить).
    /// І заборони: комбо, яке робить бота майже непереможним для людей (рішення Шидзена).
    /// </summary>
    public static class CardSynergy
    {
        /// <summary>Штраф, що гарантовано відсуває заборонену картку на останнє місце у виборі.</summary>
        public const float ForbiddenPenalty = -100f;

        private static readonly string[] Bounces = { "BOUNCY", "MAYHEM", "RICCOCHET", "REBOUND", "DANCING BULLET", "FAMILIARS FRIEND" };

        private static readonly string[] OnBlockEffects =
        {
            "BOMBS AWAY", "SAW", "SILENCE", "SUPERNOVA", "SHOCKWAVE", "FROST SLAM", "OVERPOWER", "HARMING FIELD", "EMP",
            "RADAR SHOT", "STATIC FIELD", "HEALING FIELD", "IMPLODE",
        };

        private static readonly string[] Crits = { "CLOAK AND DAGGER", "DEFENSIVE DICE", "FEROCITY", "PRECISION", "PICKPOCKETS DAGGER" };

        /// <summary>Пари «картка — партнери — бонус». Бонус діє в обидва боки.</summary>
        private static readonly (string Card, string[] Partners, float Bonus)[] Pairs =
        {
            ("TRICKSTER", Bounces, 0.8f),
            ("REFLECTION ACCELERANT", Bounces, 0.6f),
            ("TARGET BOUNCE", Bounces, 0.5f),
            ("TREASURE MAP", new[] { "COMPASS" }, 1.5f),
            ("ANGELIC", new[] { "GUARDIAN ANGEL" }, 0.8f),        // разом відкривають Heavensent
            ("INFERNAL", new[] { "BINDING CHAINS" }, 0.6f),       // Burning Binds
            ("DRUIDS BLESSING", new[] { "SAW SHOT" }, 0.6f),      // Thorn Shot
            ("MENDING RELOAD", new[] { "SPELLBOUND" }, 0.5f),     // Hexing Reload
            ("SCAREDY CAT", new[] { "FORTRESS" }, 0.5f),          // Feline Frenzy
            ("GROVETENDERS SHIELD", new[] { "SLOW START" }, 0.5f), // Grovetenders Rest
            ("EMPOWER", OnBlockEffects, 0.6f),
            ("SHIELDS UP", new[] { "RADAR SHOT" }, 0.6f),         // Шидзен: блок-збірка (ПКМ)
            ("PARASITE", new[] { "POISON" }, 0.5f),               // Шидзен: шкода з часом стакається
            ("LEECH", new[] { "POISON", "PARASITE" }, 0.4f),      // лікування від шкоди з часом
            ("PRECISION", Crits, 0.3f),
            ("PICKPOCKETS DAGGER", Crits, 0.3f),
        };

        /// <summary>Заборонені для бота комбо: SHIELDS UP + TACTICAL RELOAD — нескінченне коло «блок — перезарядка».</summary>
        private static readonly (string A, string B)[] Forbidden =
        {
            ("SHIELDS UP", "TACTICAL RELOAD"),
        };

        /// <summary>Поправка для картки <paramref name="candidate"/> з огляду на вже взяті <paramref name="owned"/>.</summary>
        public static float Adjustment(string candidate, IEnumerable<string> owned)
        {
            if (string.IsNullOrWhiteSpace(candidate) || owned == null)
            {
                return 0f;
            }

            string card = Normalize(candidate);
            var have = new HashSet<string>(owned.Where(n => !string.IsNullOrWhiteSpace(n)).Select(Normalize));
            if (IsForbidden(card, have))
            {
                return ForbiddenPenalty;
            }

            float bonus = 0f;
            foreach ((string pairCard, string[] partners, float pairBonus) in Pairs)
            {
                bool completes = pairCard == card ? partners.Any(have.Contains) : partners.Contains(card) && have.Contains(pairCard);
                if (completes)
                {
                    bonus += pairBonus;
                }
            }

            return bonus;
        }

        public static bool IsForbidden(string candidate, ISet<string> owned)
        {
            return Forbidden.Any(pair => (candidate == pair.A && owned.Contains(pair.B)) || (candidate == pair.B && owned.Contains(pair.A)));
        }

        /// <summary>Назва категорії, якою автори модів прибирають картку від ботів (конвенція RoundsWithBots).</summary>
        public const string NotForBotsCategory = "NotForBots";

        /// <summary>Картка позначена «не для ботів» — серед категорій є <see cref="NotForBotsCategory"/>.</summary>
        public static bool IsNotForBots(IEnumerable<string> categoryNames)
        {
            return categoryNames != null && categoryNames.Any(n => string.Equals(n?.Trim(), NotForBotsCategory, StringComparison.OrdinalIgnoreCase));
        }

        private static string Normalize(string name) => name.Trim().ToUpperInvariant();
    }
}
