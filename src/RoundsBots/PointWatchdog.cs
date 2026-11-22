using System.Linq;
using Photon.Pun;
using RoundsBots.Logic;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Запобіжник «вічного очка» (ШІ-чат, 2026-11-22): іде бій, живі лише наші боти (керовані цим ПК) щонайменше
    /// з двох команд, і ForceEndPointSeconds (60 с) ніхто не отримав справжньої шкоди — найслабший за часткою HP бот
    /// гине штатним RPC гри (без воскресіння), очко закінчується. Жива людина чи чужий бот (будь-де) — нічого не робимо.
    /// </summary>
    internal sealed class PointWatchdog : MonoBehaviour
    {
        private const float CheckInterval = 1f;

        private float nextCheckAt;

        private void Update()
        {
            if (Time.unscaledTime < nextCheckAt || PlayerManager.instance == null)
            {
                return;
            }

            nextCheckAt = Time.unscaledTime + CheckInterval;
            float threshold = Plugin.ForceEndSeconds.Value;
            if (threshold <= 0f || !DamageTracker.PointActive)
            {
                return;
            }

            // Людей і чужих ботів рахуємо без фільтра арени: людина за межами — теж жива (п.2).
            Player[] alive = PlayerManager.instance.players
                .Where(p => p != null && p.data != null && !p.data.dead)
                .ToArray();
            int ownBotCount = alive.Count(IsOwnBot);
            int teams = alive.Select(p => p.teamID).Distinct().Count();
            float quiet = Time.time - DamageTracker.LastDamageAt;
            if (!Stalemate.ShouldForceEnd(alive.Length - ownBotCount, ownBotCount, teams, quiet, threshold))
            {
                return;
            }

            Player weakest = alive
                .Where(p => IsOwnBot(p) && IsKillable(p))
                .OrderBy(p => p.data.health / Mathf.Max(1f, p.data.maxHealth))
                .ThenBy(p => p.playerID)
                .FirstOrDefault();
            if (weakest == null)
            {
                return;
            }

            BotLog.Write(System.FormattableString.Invariant(
                $"WATCHDOG forced-end bot={weakest.playerID} quiet={quiet:F0} lastDamageAt={DamageTracker.LastDamageAt:F0} alive={alive.Length} teams={teams}"));
            DamageTracker.MarkActivity();
            weakest.data.view.RPC("RPCA_Die", RpcTarget.All, Vector2.down);
        }

        private static bool IsOwnBot(Player player) => BotBrain.IsBot(player) && player.data.view != null && player.data.view.IsMine;

        /// <summary>Кандидат на смерть: на арені (не «припаркований»), не воскресає, активний (п.3).</summary>
        private static bool IsKillable(Player player)
        {
            Vector2 at = player.transform.position;
            bool inArena = Mathf.Abs(at.x) < BotMath.ArenaHalfWidth + 10f && Mathf.Abs(at.y) < BotMath.ArenaHalfHeight + 10f;
            return inArena && player.gameObject.activeInHierarchy && player.data.healthHandler != null && !player.data.healthHandler.isRespawning;
        }
    }
}
