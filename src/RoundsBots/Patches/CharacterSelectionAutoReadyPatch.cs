using HarmonyLib;
using UnityEngine;

namespace RoundsBots.Patches
{
    /// <summary>
    /// У локальному лобі бот сам натискає «готовий», щоб гра могла стартувати.
    /// Але не одразу: гра стартує, щойно всі готові, і боти, додані пізніше, опинялися поза матчем
    /// (без карток, без смужки карток). Тому бот чекає, доки кілька секунд ніхто новий не приєднується.
    /// </summary>
    [HarmonyPatch(typeof(CharacterSelectionInstance), "Update")]
    internal static class CharacterSelectionAutoReadyPatch
    {
        private const float QuietSecondsBeforeReady = 4f;

        private static int lastPlayerCount = -1;
        private static float lastJoinAt;

        private static void Postfix(CharacterSelectionInstance __instance)
        {
            if (__instance.isReady || !BotBrain.IsBot(__instance.currentPlayer) || PlayerManager.instance == null)
            {
                return;
            }

            int count = PlayerManager.instance.players.Count;
            if (count != lastPlayerCount)
            {
                lastPlayerCount = count;
                lastJoinAt = Time.unscaledTime;
            }

            if (Time.unscaledTime - lastJoinAt < QuietSecondsBeforeReady)
            {
                return;
            }

            __instance.ReadyUp();
        }
    }
}
