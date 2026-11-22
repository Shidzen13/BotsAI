using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RoundsBots.Patches
{
    /// <summary>
    /// Бот-проти-бота без людини: наприкінці матчу RWF показує меню CONTINUE/REMATCH/EXIT і чекає натискання.
    /// Якщо всі гравці — боти і ввімкнено [Experiment] AutoRematch, через кілька секунд обираємо REMATCH,
    /// щоб експерименти йшли безперервно. RWF — м'яка залежність, тому патч через рефлексію.
    /// </summary>
    [HarmonyPatch]
    internal static class AutoRematchPatch
    {
        private const string Rematch = "REMATCH";
        private const float DelaySeconds = 3f;

        private static Type popUpType;

        private static bool Prepare()
        {
            popUpType = AccessTools.TypeByName("RWF.UI.PopUpMenu");
            return popUpType != null;
        }

        private static MethodBase TargetMethod() => AccessTools.Method(popUpType, "Open");

        private static void Postfix(MonoBehaviour __instance, List<string> choices)
        {
            if (!Plugin.AutoRematch.Value || choices == null || !choices.Contains(Rematch))
            {
                return;
            }

            Player[] players = PlayerManager.instance.players.Where(p => p != null).ToArray();
            if (players.Length == 0 || !players.All(BotBrain.IsBot))
            {
                return; // з людиною — нехай вирішує людина
            }

            __instance.StartCoroutine(ChooseLater(__instance, choices.IndexOf(Rematch)));
        }

        private static IEnumerator ChooseLater(MonoBehaviour menu, int index)
        {
            yield return new WaitForSecondsRealtime(DelaySeconds);
            BotLog.Write("RESULT game-over auto-rematch");
            AccessTools.Method(popUpType, "SetChoice").Invoke(menu, new object[] { index });
            AccessTools.Method(popUpType, "Choose").Invoke(menu, null);
        }
    }
}
