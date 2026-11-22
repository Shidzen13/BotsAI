#nullable enable
namespace RoundsBots.Logic
{
    /// <summary>Що відомо про картку з бази docs/cards.json: наскільки вона ризикована для бота і чи діє лише впритул.</summary>
    public readonly struct CardTraits
    {
        public CardTraits(int risk, bool closeRange, CardFeatures? features = null, bool selfDamage = false)
        {
            Risk = risk;
            CloseRange = closeRange;
            Features = features;
            SelfDamage = selfDamage;
        }

        /// <summary>Картка шкодить власнику (Demonic pact — за постріл, Vampiric Block — за блок).</summary>
        public bool SelfDamage { get; }

        /// <summary>
        /// Числові ефекти з бази (витягнуті з ассетів і коду карток, зокрема те, що задається в OnAddCard
        /// і чого не видно в компонентах). Спільний екземпляр — не змінювати.
        /// </summary>
        public CardFeatures? Features { get; }

        /// <summary>0 — безпечна, 3 — висока небезпека (самошкода, телепорт, вимога стояти на місці).</summary>
        public int Risk { get; }

        public bool CloseRange { get; }
    }

    /// <summary>Поправки до оцінки картки з бази знань (за точною назвою картки).</summary>
    public static partial class CardKnowledge
    {
        private const float RiskPenalty = 0.6f;
        private const float SelfDamagePenalty = 3f;     // самошкода при швидкій стрільбі вбиває власника
        private const float CloseRangePenalty = 0.8f;   // бот за замовчуванням тримає дистанцію 13–16
        private const float CloseRangeSynergy = 0.6f;   // уже ближня збірка — такі картки підсилюють

        public static bool TryGet(string cardName, out CardTraits traits)
        {
            traits = default;
            return !string.IsNullOrEmpty(cardName) && Table.TryGetValue(cardName.Trim().ToUpperInvariant(), out traits);
        }

        private const float DuplicatePenalty = 0.7f;
        private const float UnknownModdedPenalty = 0.3f;

        /// <summary>
        /// Модова картка, якої немає в базі: її ефекти часто задані в коді мода й не видні в компонентах,
        /// тож оцінка ненадійна — невеликий мінус за невизначеність. Ванільні й відомі модові — без поправки.
        /// </summary>
        public static float OriginAdjustment(CardOrigin origin, string cardName)
        {
            return origin.IsModded && !TryGet(cardName, out _) ? -UnknownModdedPenalty : 0f;
        }

        /// <summary>
        /// Картки, що добре стакаються (аналіз виборів Шидзена + ШІ-чат 2026-11-22): POISON брав 3 копії за матч —
        /// набоїв менше 1 не стає (ApplyCardStats обрізає 1–90), тож третя копія вже нічого не забирає. PARASITE — за словами Шидзена.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> Stackable =
            new System.Collections.Generic.HashSet<string> { "POISON", "PARASITE" };

        /// <summary>Кожна вже взята копія картки знижує оцінку (крім карток, що стакаються).</summary>
        public static float DuplicateAdjustment(string cardName, int ownedCopies)
        {
            string key = cardName?.Trim().ToUpperInvariant() ?? "";
            return Stackable.Contains(key) ? 0f : -DuplicatePenalty * ownedCopies;
        }

        /// <summary>
        /// Поправка до оцінки: мінус за ризик, плюс/мінус за ближній бій залежно від збірки.
        /// Якщо самошкоду вимкнено модом (Toggle_Friendly_Fire: DisableSelfDamage), ризик таких карток зникає —
        /// лишаються самі плюси (Demonic pact: стрільба в ~40 разів швидша). Інакше — великий штраф.
        /// </summary>
        public static float Adjustment(string cardName, int ownCloseRangeCards, bool selfDamageEnabled = true)
        {
            if (!TryGet(cardName, out CardTraits traits))
            {
                return 0f;
            }

            float adjustment = !traits.SelfDamage ? -RiskPenalty * traits.Risk
                : selfDamageEnabled ? -RiskPenalty * traits.Risk - SelfDamagePenalty
                : 0f;
            if (traits.CloseRange)
            {
                adjustment += ownCloseRangeCards > 0 ? CloseRangeSynergy : -CloseRangePenalty;
            }

            return adjustment;
        }
    }
}
