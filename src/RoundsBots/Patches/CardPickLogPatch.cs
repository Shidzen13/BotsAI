using HarmonyLib;
using RoundsBots.Logic;
using UnityEngine;

namespace RoundsBots.Patches
{
    /// <summary>
    /// Логує кожен вибір картки (людина чи бот): хто, яку картку, її роль і оцінка бота.
    /// Так можна порівнювати вибір людини з оцінкою бота і вчитися, чому людина бере саме це.
    /// </summary>
    [HarmonyPatch(typeof(CardChoice), nameof(CardChoice.Pick))]
    internal static class CardPickLogPatch
    {
        private static void Prefix(CardChoice __instance, GameObject pickedCard)
        {
            if (pickedCard == null || __instance.pickrID < 0)
            {
                return;
            }

            try
            {
                Player picker = System.Linq.Enumerable.FirstOrDefault(PlayerManager.instance.players, p => p != null && p.playerID == __instance.pickrID);
                string who = picker == null ? "?" : BotBrain.IsBot(picker) ? "bot" : "human";
                CardFeatures features = CardKnowledge.TryGet(pickedCard.GetComponent<CardInfo>()?.cardName, out CardTraits t) && t.Features != null ? t.Features : CardFeatureReader.Read(pickedCard);
                string name = pickedCard.GetComponent<CardInfo>()?.cardName;
                CardOrigin origin = CardOrigin.Parse(pickedCard.name);
                string known = CardKnowledge.TryGet(name, out _) ? "db" : "unknown";
                string line = $"PICK picker={__instance.pickrID} who={who} card={name} mod={origin} known={known} role={CardScoring.RoleOf(features)} score={CardScoring.Score(features):0.00}";
                BotLog.Write(line);
                if (who == "human")
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(InputRecorder.Folder, "human_cards.txt"),
                        $">>> {line}{System.Environment.NewLine}{System.Environment.NewLine}");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"Pick log failed: {ex.Message}");
            }
        }
    }
}
