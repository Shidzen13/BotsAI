using System.Collections;
using System.Linq;
using HarmonyLib;
using UnboundLib.GameModes;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Діагностика зависань між раундами: якщо після вибору карток (PickEnd) чи кінця очка (PointEnd)
    /// за <see cref="StuckSeconds"/> с не почалось нове очко — у журнал стан мапи й кожного гравця (рядок STUCK_TRANSITION).
    /// </summary>
    internal static class TransitionDiagnostics
    {
        private const float StuckSeconds = 15f;

        private static int transition;
        private static bool pointStarted;

        public static void RegisterHooks()
        {
            GameModeManager.AddHook(GameModeHooks.HookPickEnd, handler => Watch("PickEnd"));
            GameModeManager.AddHook(GameModeHooks.HookPointEnd, handler => Watch("PointEnd"));
            GameModeManager.AddHook(GameModeHooks.HookPointStart, handler => MarkStarted());
        }

        private static IEnumerator MarkStarted()
        {
            pointStarted = true;
            yield break;
        }

        private static IEnumerator Watch(string after)
        {
            pointStarted = false;
            int id = ++transition;
            Plugin.Instance.StartCoroutine(Check(after, id));
            yield break;
        }

        private static IEnumerator Check(string after, int id)
        {
            yield return new WaitForSecondsRealtime(StuckSeconds);
            if (pointStarted || id != transition)
            {
                yield break;
            }

            BotLog.Write(System.FormattableString.Invariant($"STUCK_TRANSITION after={after} seconds={StuckSeconds} {DescribeMap()}"));
            foreach (Player player in PlayerManager.instance.players)
            {
                BotLog.Write(DescribePlayer(player));
            }
        }

        private static string DescribeMap()
        {
            Map map = MapManager.instance?.currentMap?.Map;
            if (map == null)
            {
                return "map=none";
            }

            bool ready = Traverse.Create(map).Field("hasCalledReady").GetValue<bool>();
            int colliders = map.GetComponentsInChildren<Collider2D>().Count(c => c.enabled);
            return System.FormattableString.Invariant($"map={map.name} level={MapManager.instance.currentLevelID} ready={ready} colliders={colliders} spawns={MapManager.instance.GetSpawnPoints()?.Length ?? -1}");
        }

        private static string DescribePlayer(Player player)
        {
            if (player == null)
            {
                return "  player=null";
            }

            CharacterData data = player.data;
            Vector3 pos = player.transform.position;
            return System.FormattableString.Invariant(
                $"  player={player.playerID} team={player.teamID} mine={data?.view?.IsMine} bot={BotLook.IsBot(player)} playing={data?.isPlaying} dead={data?.dead} active={player.gameObject.activeInHierarchy} pos=({pos.x:0.0},{pos.y:0.0})");
        }
    }
}
