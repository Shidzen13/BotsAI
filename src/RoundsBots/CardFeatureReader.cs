using RoundsBots.Logic;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>Витягує з об'єкта картки її числові ефекти для <see cref="CardScoring"/>.</summary>
    internal static class CardFeatureReader
    {
        public static CardFeatures Read(GameObject card)
        {
            var features = new CardFeatures();
            if (card == null)
            {
                return features;
            }

            CardInfo info = card.GetComponent<CardInfo>();
            if (info != null)
            {
                features.Rarity = (int)info.rarity;
            }

            Gun gun = card.GetComponent<Gun>();
            if (gun != null)
            {
                features.DamageMultiplier = gun.damage;
                features.AttackSpeedMultiplier = gun.attackSpeed;
                features.ReloadTimeMultiplier = gun.reloadTime;
                features.AmmoAdd = gun.ammo;
                features.ProjectilesAdd = gun.numberOfProjectiles;
                features.Reflects = gun.reflects;
            }

            CharacterStatModifiers stats = card.GetComponent<CharacterStatModifiers>();
            if (stats != null)
            {
                features.HealthMultiplier = stats.health;
                features.MoveSpeedMultiplier = stats.movementSpeed;
                features.LifeSteal = stats.lifeSteal;
                features.Regen = stats.regen;
                features.Respawns = stats.respawns;
            }

            // Як і ApplyCardStats, шукаємо Block і в дочірніх об'єктах картки.
            Block block = card.GetComponentInChildren<Block>();
            if (block != null)
            {
                features.BlockCooldownMultiplier = block.cdMultiplier;
                features.ExtraBlocks = block.additionalBlocks;
            }

            return features;
        }
    }
}
