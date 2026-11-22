using HarmonyLib;

namespace RoundsBots
{
    /// <summary>
    /// Сумісність з модом Infoholic (панель статистики): він бере «своїм» останнього персонажа з view.IsMine —
    /// з ботами онлайн це бот, тож показувалась стата бота. Тут — локальна людина.
    /// </summary>
    internal static class InfoholicCompat
    {
        public static void Apply(Harmony harmony)
        {
            var status = AccessTools.TypeByName("Infoholic.MonoBehaviours.GameStatusUpdate");
            var method = status == null ? null : AccessTools.Method(status, "getLocalPlayer");
            if (method == null)
            {
                return;
            }

            harmony.Patch(method, prefix: new HarmonyMethod(AccessTools.Method(typeof(InfoholicCompat), nameof(LocalHuman))));
            Plugin.Log.LogInfo("Infoholic: stats show the local human (bots ignored).");
        }

        private static bool LocalHuman(ref Player __result)
        {
            if (PlayerManager.instance == null)
            {
                return true;
            }

            __result = HiddenCardsCompat.LocalViewer();
            return false;
        }
    }
}
