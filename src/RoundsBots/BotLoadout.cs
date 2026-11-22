using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Що зараз «вміє» бот за взятими картками: дальність кулі, ефекти ближнього бою на блок (ПКМ),
    /// стан перезарядки. Базові значення фіксуються при появі, щоб рахувати лише додане картками.
    /// </summary>
    internal sealed class BotLoadout
    {
        private static readonly FieldInfo CurrentAmmoField = AccessTools.Field(typeof(GunAmmo), "currentAmmo");
        private static readonly FieldInfo ReloadCounterField = AccessTools.Field(typeof(GunAmmo), "reloadCounter");

        private readonly CharacterData data;
        private readonly int baseBlockSpawns;
        private readonly int baseTriggers;
        private GunAmmo ammo;

        public BotLoadout(CharacterData data)
        {
            this.data = data;
            baseBlockSpawns = CountBlockSpawns();
            baseTriggers = CountTriggers();
        }

        private Gun Gun => data.weaponHandler != null ? data.weaponHandler.gun : null;

        /// <summary>Скільки ефектів ближнього бою додали картки (ударна хвиля на блок, аура в радіусі тощо).</summary>
        public int CloseRangeEffects => Mathf.Max(0, CountBlockSpawns() - baseBlockSpawns) + Mathf.Max(0, CountTriggers() - baseTriggers);

        /// <summary>Дальність польоту кулі; 0 — не обмежена.</summary>
        public float BulletRange(float bulletSpeed)
        {
            Gun gun = Gun;
            return gun != null && gun.destroyBulletAfter > 0f ? gun.destroyBulletAfter * bulletSpeed : 0f;
        }

        /// <summary>Скільки ще чекати до кінця перезарядки (0 — зброя готова).</summary>
        public float ReloadRemaining()
        {
            Gun gun = Gun;
            if (gun == null || !gun.isReloading)
            {
                return 0f; // лічильник іде лише під час перезарядки; інакше може «зависнути» додатним
            }

            if (ammo == null)
            {
                ammo = gun.GetComponentInChildren<GunAmmo>();
            }

            if (ammo == null || CurrentAmmoField == null || ReloadCounterField == null)
            {
                return gun.isReloading ? 1f : 0f;
            }

            int current = (int)CurrentAmmoField.GetValue(ammo);
            return current > 0 ? 0f : Mathf.Max(0f, (float)ReloadCounterField.GetValue(ammo));
        }

        private int CountBlockSpawns() => data.block != null ? data.block.objectsToSpawn.Count : 0;

        private int CountTriggers()
        {
            return data.GetComponentsInChildren<BlockTrigger>(true).Length
                + data.GetComponentsInChildren<PlayerInRangeTrigger>(true).Length;
        }
    }
}
