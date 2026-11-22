using System.Linq;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Сумісність з модом Hidden Cards (AncientKoala). Він вважає «тобою» першого персонажа з view.IsMine — з ботами
    /// онлайн це бот, тож мод ховав твої картки за «?» і чистив твою панель карток. Тут «ти» — локальна людина (не бот).
    /// </summary>
    internal static class HiddenCardsCompat
    {
        // Типи й поля шукаємо один раз: AccessTools.TypeByName перебирає всі збірки (повільно + попередження в журнал).
        private static FieldInfo tempEnableField;
        private static FieldInfo instanceField;
        private static FieldInfo gameGoingField;
        private static MethodInfo testCardMethod;
        private static FieldInfo cardBarsInstanceField;
        private static MethodInfo playersCardBarMethod;

        public static void Apply(Harmony harmony)
        {
            var hc = AccessTools.TypeByName("HiddenCards.HC");
            var choicePatch = AccessTools.TypeByName("UniqueCardChoicePatch");
            var cardBarUtils = AccessTools.TypeByName("ModdingUtils.Utils.CardBarUtils");
            Plugin.Log.LogInfo($"Hidden Cards compat: HC={hc != null} choicePatch={choicePatch != null} cardBars={cardBarUtils != null}");
            if (hc == null || choicePatch == null)
            {
                return;
            }

            tempEnableField = AccessTools.Field(hc, "tempEnable");
            instanceField = AccessTools.Field(hc, "instance");
            gameGoingField = AccessTools.Field(hc, "gameGoing");
            testCardMethod = AccessTools.Method(hc, "TestCard");
            cardBarsInstanceField = cardBarUtils == null ? null : AccessTools.Field(cardBarUtils, "instance");
            playersCardBarMethod = cardBarUtils == null ? null : AccessTools.Method(cardBarUtils, "PlayersCardBar", new[] { typeof(int) });

            harmony.Patch(AccessTools.Method(hc, "Update"), prefix: new HarmonyMethod(AccessTools.Method(typeof(HiddenCardsCompat), nameof(ClearEnemyBars))));
            harmony.Patch(AccessTools.Method(choicePatch, "RPC_HideHiddenCards"), prefix: new HarmonyMethod(AccessTools.Method(typeof(HiddenCardsCompat), nameof(HideEnemyChoice))));
            Plugin.Log.LogInfo("Hidden Cards: local human is the viewer (bots ignored).");
        }

        /// <summary>Локальна людина; якщо на цьому ПК лише боти — перший свій персонаж (як у Hidden Cards).</summary>
        internal static Player LocalViewer()
        {
            var mine = PlayerManager.instance.players.Where(p => p != null && p.data?.view != null && p.data.view.IsMine).ToList();
            return mine.FirstOrDefault(p => !BotLook.IsBot(p)) ?? mine.FirstOrDefault();
        }

        private static bool ClearEnemyBars()
        {
            object instance = instanceField.GetValue(null);
            bool enabled = (bool)tempEnableField.GetValue(null);
            bool going = instance != null && (bool)gameGoingField.GetValue(instance);
            Player viewer = enabled && going ? LocalViewer() : null;
            if (viewer == null)
            {
                return false;
            }

            // ModdingUtils — залежність Hidden Cards, не наша, тому через рефлексію.
            object cardBars = cardBarsInstanceField?.GetValue(null);
            if (cardBars == null)
            {
                return false;
            }

            foreach (Player player in PlayerManager.instance.players.Where(p => p.teamID != viewer.teamID))
            {
                (playersCardBarMethod.Invoke(cardBars, new object[] { player.playerID }) as CardBar)?.ClearBar();
            }

            return false;
        }

        private static int lastLoggedPicker = -1;

        private static bool HideEnemyChoice(int targetCardID, int pickID)
        {
            Player viewer = LocalViewer();
            Player picker = PlayerManager.instance.players.FirstOrDefault(p => p.playerID == pickID);
            PhotonView card = PhotonNetwork.GetPhotonView(targetCardID);
            bool hide = viewer != null && picker != null && card != null && viewer.teamID != picker.teamID;
            if (pickID != lastLoggedPicker)
            {
                lastLoggedPicker = pickID;
                BotLog.Write($"HIDDENCARDS viewer={viewer?.playerID} viewerTeam={viewer?.teamID} picker={pickID} pickerTeam={picker?.teamID} hide={hide}");
            }

            if (!hide)
            {
                return false;
            }

            testCardMethod.Invoke(null, new object[] { card.gameObject });
            return false;
        }
    }
}
