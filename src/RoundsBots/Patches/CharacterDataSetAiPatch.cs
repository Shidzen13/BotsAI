using GeneralInputType = GeneralInput.InputType;
using HarmonyLib;
using InControl;

namespace RoundsBots.Patches
{
    /// <summary>
    /// Ванільна гра створює AI-гравця через <c>PlayerAssigner.CreatePlayer(null, isAI: true)</c> (клавіша B у локальному лобі),
    /// яке викликає <see cref="CharacterData.SetAI"/> без «господаря». Тут ми перетворюємо такого гравця на нашого бота.
    /// Міньйони (SetAI з господарем) не чіпаємо.
    /// </summary>
    [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.SetAI))]
    internal static class CharacterDataSetAiPatch
    {
        private static void Postfix(CharacterData __instance, Player aiMaster)
        {
            if (aiMaster != null || __instance.view == null || !__instance.view.IsMine)
            {
                return;
            }

            // SetAI вмикає PlayerAPI, який обнуляє ввід щокадру — вимикаємо, бо вводом керує BotBrain.
            PlayerAPI api = __instance.GetComponent<PlayerAPI>();
            if (api != null)
            {
                api.enabled = false;
            }

            // Інтерфейс лобі та вибору карток очікує, що в гравця є PlayerActions.
            // Даємо «порожній» контролер, до якого не підключено жодного реального пристрою.
            if (__instance.playerActions == null)
            {
                PlayerActions actions = PlayerActions.CreateWithControllerBindings();
                actions.Device = new InputDevice("RoundsBot");
                __instance.playerActions = actions;
            }

            __instance.input.inputType = GeneralInputType.Controller;

            if (__instance.GetComponent<BotBrain>() == null)
            {
                __instance.gameObject.AddComponent<BotBrain>();
            }
        }
    }
}
