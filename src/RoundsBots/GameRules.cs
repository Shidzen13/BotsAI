using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Правила, які змінюють інші моди і які впливають на вибір карток.
    /// Toggle_Friendly_Fire (Pykess): DisableSelfDamage — власні кулі й самошкода карток не ранять гравця.
    /// </summary>
    internal static class GameRules
    {
        private const string FriendlyFireModId = "pykess.rounds.plugins.togglefriendlyfire";
        private const string FriendlyFireSection = "ToggleFriendlyFire";
        private const string SelfDamageKey = "DisableSelfDamage";
        private const float CacheSeconds = 5f;

        private static bool selfDamageEnabled = true;
        private static float checkedAt = float.NegativeInfinity;

        /// <summary>Чи шкодить гравцю власна самошкода (без мода — так, як у ванільній грі).</summary>
        public static bool SelfDamageEnabled
        {
            get
            {
                if (Time.unscaledTime - checkedAt >= CacheSeconds || Time.unscaledTime < checkedAt)
                {
                    checkedAt = Time.unscaledTime;
                    selfDamageEnabled = !ReadFriendlyFireFlag(SelfDamageKey);
                }

                return selfDamageEnabled;
            }
        }

        private static bool ReadFriendlyFireFlag(string key)
        {
            if (!Chainloader.PluginInfos.TryGetValue(FriendlyFireModId, out BepInEx.PluginInfo info) || info.Instance == null)
            {
                return false;
            }

            ConfigFile config = info.Instance.Config;
            return config.TryGetEntry(FriendlyFireSection, key, out ConfigEntry<bool> entry) && entry.Value;
        }
    }
}
