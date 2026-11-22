using System;
using System.Collections.Generic;

namespace RoundsBots.Logic
{
    /// <summary>
    /// Числові характеристики картки, витягнуті з її компонентів (Gun, CharacterStatModifiers, Block).
    /// Множники за замовчуванням = 1, додатки = 0 — тобто картка «нічого не змінює».
    /// </summary>
    public sealed class CardFeatures
    {
        public int Rarity { get; set; }
        public float DamageMultiplier { get; set; } = 1f;
        public float AttackSpeedMultiplier { get; set; } = 1f;
        public float ReloadTimeMultiplier { get; set; } = 1f;
        public int AmmoAdd { get; set; }
        public int ProjectilesAdd { get; set; }
        public float HealthMultiplier { get; set; } = 1f;
        public float MoveSpeedMultiplier { get; set; } = 1f;
        public float LifeSteal { get; set; }
        public float Regen { get; set; }
        public int Respawns { get; set; }
        public float BlockCooldownMultiplier { get; set; } = 1f;
        public int ExtraBlocks { get; set; }
        public int Reflects { get; set; }   // рикошети кулі від стін
        public float PlayerSizeMultiplier { get; set; } = 1f;   // менший гравець — у нього важче влучити
        public int ExtraJumps { get; set; }

        /// <summary>Оцінка особливих ефектів, яких не бачать поля вище (вибухи, криты, аури, самонаведення…) — з docs/cards.json.</summary>
        public float SpecialValue { get; set; }
    }

    /// <summary>Роль картки у збірці — для балансу (не лише HP чи лише шкода).</summary>
    public enum CardRole
    {
        None,
        Offense,
        Defense,
        Utility,
    }

    public static class CardScoring
    {
        /// <summary>Оцінка корисності картки. Чим більше — тим краща.</summary>
        public static float Score(CardFeatures card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            float score = card.Rarity * 0.6f;
            score += Log(card.DamageMultiplier) * 3f;
            score += Log(Inverse(card.AttackSpeedMultiplier)) * 2f;   // attackSpeed — це затримка, менше = краще
            score += Log(Inverse(card.ReloadTimeMultiplier)) * 1.5f;
            score += card.AmmoAdd * 0.1f;
            score += card.ProjectilesAdd * 0.8f;
            score += Log(card.HealthMultiplier) * 2.5f;
            score += Log(card.MoveSpeedMultiplier) * 1.5f;
            score += card.LifeSteal * 2f;
            score += card.Regen * 0.05f;
            score += card.Respawns * 3f;
            score += Log(Inverse(card.BlockCooldownMultiplier)) * 1.5f;
            score += card.ExtraBlocks * 1.2f;
            score += card.Reflects * 0.4f;
            score += Log(Inverse(card.PlayerSizeMultiplier)) * 2f;
            score += card.ExtraJumps * 0.8f;
            score += card.SpecialValue;
            return score;
        }

        private const float BalanceWeight = 1.5f;

        /// <summary>Роль картки — та, що дає найбільший внесок у її оцінку. None, якщо картка нічого корисного не дає.</summary>
        public static CardRole RoleOf(CardFeatures card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            float offense = Log(card.DamageMultiplier) * 3f + Log(Inverse(card.AttackSpeedMultiplier)) * 2f
                + card.ProjectilesAdd * 0.8f + card.Reflects * 0.4f;
            float defense = Log(card.HealthMultiplier) * 2.5f + card.LifeSteal * 2f + card.Regen * 0.05f + card.Respawns * 3f
                + Log(Inverse(card.BlockCooldownMultiplier)) * 1.5f + card.ExtraBlocks * 1.2f;
            float utility = Log(Inverse(card.ReloadTimeMultiplier)) * 1.5f + card.AmmoAdd * 0.1f + Log(card.MoveSpeedMultiplier) * 1.5f
                + Log(Inverse(card.PlayerSizeMultiplier)) * 2f + card.ExtraJumps * 0.8f;

            float best = Math.Max(offense, Math.Max(defense, utility));
            if (best <= 0f)
            {
                return CardRole.None;
            }

            return best == offense ? CardRole.Offense : best == defense ? CardRole.Defense : CardRole.Utility;
        }

        /// <summary>
        /// Поправка за баланс збірки: бонус ролі, якої у вже взятих картках мало, штраф — якої забагато.
        /// <paramref name="owned"/> — ролі карток, які гравець уже має.
        /// </summary>
        public static float BalanceAdjustment(CardRole role, IReadOnlyCollection<CardRole> owned)
        {
            if (role == CardRole.None || owned == null)
            {
                return 0f;
            }

            int total = 0;
            int same = 0;
            foreach (CardRole r in owned)
            {
                if (r == CardRole.None)
                {
                    continue;
                }

                total++;
                if (r == role)
                {
                    same++;
                }
            }

            if (total == 0)
            {
                return 0f;
            }

            const float Expected = 1f / 3f;
            return (Expected - (float)same / total) * BalanceWeight;
        }

        /// <summary>
        /// Обирає індекс найкращої картки. <paramref name="noise"/> — випадкові числа 0..1 (по одному на картку),
        /// масштабуються на <paramref name="randomness"/>. Порожній список → -1.
        /// </summary>
        public static int ChooseIndex(IReadOnlyList<float> scores, IReadOnlyList<float> noise, float randomness)
        {
            if (scores == null || scores.Count == 0)
            {
                return -1;
            }

            int best = 0;
            float bestValue = float.NegativeInfinity;
            for (int i = 0; i < scores.Count; i++)
            {
                float n = noise != null && i < noise.Count ? noise[i] : 0f;
                float value = scores[i] + n * randomness;
                if (value > bestValue)
                {
                    bestValue = value;
                    best = i;
                }
            }

            return best;
        }

        private static float Inverse(float multiplier) => multiplier > 0.0001f ? 1f / multiplier : 1f;

        private static float Log(float value) => value > 0.0001f ? (float)Math.Log(value) : 0f;
    }
}
