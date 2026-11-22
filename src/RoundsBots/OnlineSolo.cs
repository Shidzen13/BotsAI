using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using PhotonPlayer = Photon.Realtime.Player;
using Room = Photon.Realtime.Room;

namespace RoundsBots
{
    /// <summary>
    /// Гра в онлайн-кімнаті RWF самому з ботами (ШІ-чат, 2026-11-22):
    /// 1) дві перевірки RWF «клієнтів у кімнаті більше одного» (старт і скасування відліку) рахують наших ботів як ще одного;
    /// 2) ліміт персонажів на ПК — з конфігу [Online] MaxCharactersPerClient (2..5); значення оголошуємо в кімнаті,
    ///    і якщо в когось інше (або немає мода при ліміті > 2) — попередження, B не додає ботів;
    /// 3) RWF SetCharacter створює масив персонажів «new LobbyCharacter[2]» — замінюємо 2 на ліміт;
    /// 4) у кімнаті гасимо ванільні клавіші: LeftShift (ArtHandler — зміна оформлення мапи) і 2/4 (GM_ArmsRace — maxPlayers),
    ///    бо цифри й Shift+цифри керують кольором ботів.
    /// </summary>
    internal static class OnlineSolo
    {
        private const string LimitPropertyKey = "rbMaxChars";
        private const int RwfDefaultLimit = 2;

        /// <summary>Скільки наших ботів зараз у кімнаті (оновлює <see cref="OnlineBots"/>).</summary>
        public static int OwnBotsInRoom { get; set; }

        /// <summary>Режим «сам з ботами» не вдалося увімкнути (RWF змінився) — пояснення для підказки на екрані.</summary>
        public static string SoloUnavailableReason { get; private set; }

        public static int ConfiguredLimit => Plugin.MaxCharactersPerClient.Value;

        public static void Apply(Harmony harmony)
        {
            Type handler = AccessTools.TypeByName("RWF.PrivateRoomHandler");
            Type mod = AccessTools.TypeByName("RWF.RWFMod");
            var transpiler = new HarmonyMethod(AccessTools.Method(typeof(OnlineSolo), nameof(CountBotsAsClient)));
            int patched = 0;
            foreach (MethodBase target in new MethodBase[] { AccessTools.PropertyGetter(handler, "GameCanStart"), AccessTools.Method(handler, "Update") })
            {
                if (target != null)
                {
                    harmony.Patch(target, transpiler: transpiler);
                    patched++;
                }
            }

            if (patched < 2 || replacements < 2)
            {
                SoloUnavailableReason = "solo play unavailable: RWF changed (see LogOutput)";
                Plugin.Log.LogWarning($"Online solo: patched methods={patched}, PlayerCount checks replaced={replacements}");
            }

            MethodInfo limitGetter = AccessTools.PropertyGetter(mod, "MaxCharactersPerClient");
            if (limitGetter != null)
            {
                harmony.Patch(limitGetter, postfix: new HarmonyMethod(AccessTools.Method(typeof(OnlineSolo), nameof(OverrideLimit))));
            }

            PatchSetCharacter(harmony);
            harmony.Patch(
                AccessTools.Method(typeof(MapManager), "ReportMapLoaded"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(OnlineSolo), nameof(CountOwnMapLoad))));
            var skipInRoom = new HarmonyMethod(AccessTools.Method(typeof(OnlineSolo), nameof(SkipVanillaKeysInRoom)));
            foreach (string typeName in new[] { "ArtHandler", "GM_ArmsRace" })
            {
                MethodInfo update = AccessTools.Method(AccessTools.TypeByName(typeName), "Update");
                if (update != null)
                {
                    harmony.Patch(update, prefix: skipInRoom);
                }
            }

            Plugin.Log.LogInfo($"Online solo: PlayerCount checks replaced={replacements}, SetCharacter array size patched={arraySizeReplacements}, MaxCharactersPerClient={ConfiguredLimit}");
        }

        private static int replacements;
        private static int arraySizeReplacements;

        private static bool SkipVanillaKeysInRoom() => !OnlineBots.RoomOpen;

        private static readonly AccessTools.FieldRef<MapManager, int> OthersLoadedLevel =
            AccessTools.FieldRefAccess<MapManager, int>("otherPlayersMostRecentlyLoadedLevel");

        /// <summary>
        /// Мапа онлайн стартує, коли «інші гравці» повідомили, що її завантажили (Map.LoadedForAll). Сам у кімнаті —
        /// інших немає, і мапа чекала вічно. Тоді зараховуємо власне завантаження.
        /// </summary>
        private static void CountOwnMapLoad(MapManager __instance, int levelID)
        {
            if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount <= 1)
            {
                OthersLoadedLevel(__instance) = levelID;
            }
        }

        private static void PatchSetCharacter(Harmony harmony)
        {
            Type extensions = AccessTools.TypeByName("RWF.PhotonPlayerExtensions");
            MethodInfo target = extensions == null ? null : AccessTools.Method(extensions, "SetCharacter");
            if (target != null)
            {
                harmony.Patch(target, transpiler: new HarmonyMethod(AccessTools.Method(typeof(OnlineSolo), nameof(UseLimitForArray))));
            }

            if (arraySizeReplacements != 1 && ConfiguredLimit > RwfDefaultLimit)
            {
                SoloUnavailableReason = "limit > 2 unavailable: RWF changed - set MaxCharactersPerClient = 2";
                Plugin.Log.LogWarning($"Online solo: SetCharacter array size replacements={arraySizeReplacements}");
            }
        }

        /// <summary>«ldc.i4.2; newarr LobbyCharacter» → «call ConfiguredLimit; newarr LobbyCharacter».</summary>
        private static IEnumerable<CodeInstruction> UseLimitForArray(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();
            for (int i = 0; i + 1 < code.Count; i++)
            {
                bool isTwo = code[i].opcode == System.Reflection.Emit.OpCodes.Ldc_I4_2;
                bool nextIsCharacterArray = code[i + 1].opcode == System.Reflection.Emit.OpCodes.Newarr
                    && (code[i + 1].operand as Type)?.Name == "LobbyCharacter";
                if (isTwo && nextIsCharacterArray)
                {
                    code[i] = new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.PropertyGetter(typeof(OnlineSolo), nameof(ConfiguredLimit))).MoveLabelsFrom(code[i]);
                    arraySizeReplacements++;
                }
            }

            return code;
        }

        private static IEnumerable<CodeInstruction> CountBotsAsClient(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                // Room.PlayerCount є і в Room, і в базовому RoomInfo (new) — AccessTools.PropertyGetter тут неоднозначний.
                if (instruction.operand is MethodInfo called && called.Name == "get_PlayerCount" && typeof(Photon.Realtime.RoomInfo).IsAssignableFrom(called.DeclaringType))
                {
                    replacements++;
                    string replacement = called.ReturnType == typeof(byte) ? nameof(EffectivePlayerCount) : nameof(EffectivePlayerCountInt);
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(OnlineSolo), replacement)).MoveLabelsFrom(instruction);
                    continue;
                }

                yield return instruction;
            }
        }

        /// <summary>Клієнтів у кімнаті + 1, якщо в ній є наші боти (лише для перевірок старту RWF).</summary>
        public static int EffectivePlayerCountInt(Photon.Realtime.RoomInfo room) => EffectivePlayerCount(room);

        public static byte EffectivePlayerCount(Photon.Realtime.RoomInfo room)
        {
            int count = room is Room current ? current.PlayerCount : room != null ? room.PlayerCount : 0;
            return (byte)Math.Min(byte.MaxValue, count + (OwnBotsInRoom > 0 && count == 1 ? 1 : 0));
        }

        private static void OverrideLimit(ref int __result)
        {
            __result = ConfiguredLimit;
        }

        /// <summary>Оголосити свій ліміт у кімнаті (раз, коли змінився).</summary>
        public static void Announce()
        {
            PhotonPlayer local = PhotonNetwork.LocalPlayer;
            if (local == null || PhotonNetwork.CurrentRoom == null)
            {
                return;
            }

            if (!(local.CustomProperties[LimitPropertyKey] is int announced) || announced != ConfiguredLimit)
            {
                local.SetCustomProperties(new Hashtable { [LimitPropertyKey] = ConfiguredLimit });
            }
        }

        /// <summary>
        /// Чи безпечно додавати ботів: при ліміті 2 — завжди (другу мод не потрібен); при більшому — лише якщо всі інші
        /// мають мод з тим самим лімітом (інакше номери персонажів у RWF збігаються й кімната ламається).
        /// </summary>
        public static bool PeersCompatible(out string problem)
        {
            problem = null;
            if (ConfiguredLimit == RwfDefaultLimit || PhotonNetwork.CurrentRoom == null)
            {
                return true;
            }

            PhotonPlayer other = PhotonNetwork.PlayerListOthers.FirstOrDefault(p => !(p.CustomProperties[LimitPropertyKey] is int limit) || limit != ConfiguredLimit);
            if (other == null)
            {
                return true;
            }

            problem = $"{other.NickName}: no BotsAI or a different limit. Glitches possible - to play with them, leave the room and set [Online] MaxCharactersPerClient = 2";
            return false;
        }
    }
}
