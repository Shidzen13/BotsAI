using TMPro;
using UnboundLib;
using UnboundLib.Utils.UI;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>Меню «BotsAI» у налаштуваннях модів гри (Unbound). Значення одразу пишуться в конфіг BepInEx.</summary>
    internal static class BotsMenu
    {
        private static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard" };

        public static void Register()
        {
            Unbound.RegisterMenu(Plugin.ModName, () => { }, Build, null, true);
        }

        private static void Build(GameObject menu)
        {
            MenuHandler.CreateText("<b>BotsAI " + Plugin.ModVersion, menu, out TextMeshProUGUI _, 70);
            MenuHandler.CreateText("B add bot, N remove, 1-4 bot color", menu, out TextMeshProUGUI _, 30);
            Blank(menu, 40);

            MenuHandler.CreateText(DifficultyLabel(), menu, out TextMeshProUGUI difficultyText, 40);
            MenuHandler.CreateSlider("Difficulty", menu, 30, 0, 2, (int)Plugin.Difficulty.Value, value =>
            {
                Plugin.Difficulty.Value = (Logic.BotDifficulty)Mathf.RoundToInt(value);
                difficultyText.text = DifficultyLabel();
            }, out UnityEngine.UI.Slider _, true);
            Blank(menu, 20);

            MenuHandler.CreateSlider("Card pick delay (s)", menu, 30, 0.1f, 5f, Plugin.CardPickDelay.Value,
                value => Plugin.CardPickDelay.Value = value, out UnityEngine.UI.Slider _);
            Blank(menu, 20);

            MenuHandler.CreateSlider("Stalemate: end point after (s, 0 = off)", menu, 30, 0f, 600f, Plugin.ForceEndSeconds.Value,
                value => Plugin.ForceEndSeconds.Value = value, out UnityEngine.UI.Slider _, true);
            Blank(menu, 20);

            MenuHandler.CreateSlider("Online: characters per PC (you + bots)", menu, 30, 2, 5, Plugin.MaxCharactersPerClient.Value,
                value => Plugin.MaxCharactersPerClient.Value = Mathf.RoundToInt(value), out UnityEngine.UI.Slider _, true);
            MenuHandler.CreateText("2 is safe with friends without BotsAI. Change before joining a room.", menu, out TextMeshProUGUI _, 25);
            Blank(menu, 20);

            MenuHandler.CreateToggle(Plugin.AutoRematch.Value, "Auto rematch when only bots play", menu,
                value => Plugin.AutoRematch.Value = value, 30);
        }

        private static string DifficultyLabel()
        {
            int index = Mathf.Clamp((int)Plugin.Difficulty.Value, 0, DifficultyNames.Length - 1);
            return "Bot difficulty: <b>" + DifficultyNames[index];
        }

        private static void Blank(GameObject menu, int size)
        {
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, size);
        }
    }
}
