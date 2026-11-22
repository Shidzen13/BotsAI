using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using RoundsBots.Logic;
using UnboundLib.GameModes;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Остання шкода кожного гравця (хто, чим, від чого) — щоб рядок RESULT died казав причину смерті.
    /// Плюс номер матчу (хук UnboundLib «GameStart») — варіант A/B тримається весь матч.
    /// </summary>
    [HarmonyPatch(typeof(HealthHandler), nameof(HealthHandler.DoDamage))]
    internal static class DamageTracker
    {
        internal readonly struct Hit
        {
            public Hit(DamageCause cause, int sourceId, string weapon, float time)
            {
                Cause = cause;
                SourceId = sourceId;
                Weapon = weapon;
                Time = time;
            }

            public DamageCause Cause { get; }
            public int SourceId { get; }
            public string Weapon { get; }
            public float Time { get; }
        }

        private static readonly Dictionary<int, Hit> LastHit = new Dictionary<int, Hit>();

        /// <summary>Коли будь-хто востаннє отримав шкоду (або почалося очко) — для розриву пата.</summary>
        public static float LastDamageAt { get; private set; }

        /// <summary>Іде бій (між PointStart і PointEnd/GameEnd/PickStart) — лише тоді працює запобіжник.</summary>
        public static bool PointActive { get; private set; }

        /// <summary>Номер матчу з моменту запуску гри (0 до першого старту).</summary>
        public static int MatchIndex { get; private set; }

        public static void RegisterHooks()
        {
            GameModeManager.AddHook(GameModeHooks.HookGameStart, OnGameStart);
            GameModeManager.AddHook(GameModeHooks.HookPointStart, OnPointStart);
            GameModeManager.AddHook(GameModeHooks.HookPointEnd, OnBattleStop);
            GameModeManager.AddHook(GameModeHooks.HookGameEnd, OnBattleStop);
            GameModeManager.AddHook(GameModeHooks.HookPickStart, OnBattleStop);
        }

        private static IEnumerator OnGameStart(IGameModeHandler handler)
        {
            MatchIndex++;
            LastHit.Clear();
            BotLog.Write($"MATCH start index={MatchIndex}");
            yield break;
        }

        private static IEnumerator OnPointStart(IGameModeHandler handler)
        {
            LastDamageAt = UnityEngine.Time.time;
            PointActive = true;
            yield break;
        }

        private static IEnumerator OnBattleStop(IGameModeHandler handler)
        {
            PointActive = false;
            yield break;
        }

        /// <summary>Запобіжник завершив очко — тиша рахується заново (наступна перевірка не одразу).</summary>
        public static void MarkActivity() => LastDamageAt = UnityEngine.Time.time;

        public static bool TryGetLastHit(int playerId, out Hit hit) => LastHit.TryGetValue(playerId, out hit);

        private static void Prefix(HealthHandler __instance, Vector2 damage, GameObject damagingWeapon, Player damagingPlayer, bool ignoreBlock)
        {
            Player victim = __instance != null ? __instance.GetComponent<Player>() : null;
            if (victim == null || damage == Vector2.zero)
            {
                return;
            }

            // Ті самі умови, що на початку DoDamage: заблокований чи «порожній» удар — не шкода (ШІ-чат, п.5).
            CharacterData data = victim.data;
            if (data == null || !data.isPlaying || data.dead || __instance.isRespawning
                || (data.block != null && data.block.IsBlocking() && !ignoreBlock))
            {
                return;
            }

            Vector2 at = victim.transform.position;
            int sourceId = damagingPlayer != null ? damagingPlayer.playerID : -1;
            DamageCause cause = DeathCause.Classify(sourceId, victim.playerID, at.x, at.y);
            string weapon = damagingWeapon != null ? damagingWeapon.name.Replace("(Clone)", string.Empty) : "-";
            LastHit[victim.playerID] = new Hit(cause, sourceId, weapon, UnityEngine.Time.time);
            LastDamageAt = UnityEngine.Time.time;
        }
    }
}
