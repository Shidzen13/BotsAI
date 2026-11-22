using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

namespace RoundsBots
{
    /// <summary>
    /// Вигляд ботів: ванільне обличчя (стандартні очі й рот, без прикрас) замість обличчя гравця,
    /// і ім'я «Бот N» у лобі RWF замість «нік N». Ботів в онлайні позначає властивість гравця <see cref="BotsKey"/>
    /// (локальні номери його ботів) — її бачать усі, у кого є BotsAI.
    /// </summary>
    internal static class BotLook
    {
        public const string BotsKey = "rbBots";

        private static readonly MethodInfo AdditionalDataMethod = AccessTools.Method(AccessTools.TypeByName("RWF.PlayerExtensions"), "GetAdditionalData");
        private static readonly FieldInfo LocalIdField = AccessTools.Field(AccessTools.TypeByName("RWF.PlayerAdditionalData"), "localID");
        private static readonly Type SelectorType = AccessTools.TypeByName("RWF.UI.PrivateRoomCharacterSelectionInstance");
        private static readonly System.Collections.Generic.HashSet<int> VanillaApplied = new System.Collections.Generic.HashSet<int>();

        /// <summary>Ванільне обличчя — порожній пресет: очі-крапки, рот-риска, без прикрас (усі id і зсуви — нулі).</summary>
        private static PlayerFace VanillaFace => new PlayerFace();

        public static void Apply(Harmony harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(Player), nameof(Player.RPCA_SetFace)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(BotLook), nameof(VanillaFaceForBots))));
            harmony.Patch(
                AccessTools.Method(typeof(CharacterCreatorItemEquipper), nameof(CharacterCreatorItemEquipper.RPCA_SetFace)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(BotLook), nameof(VanillaPortraitForBots))));
            harmony.Patch(
                AccessTools.Method(typeof(CardChoiceVisuals), nameof(CardChoiceVisuals.Show)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(BotLook), nameof(VanillaFaceWhenBotPicks))));

            MethodInfo nickName = AccessTools.PropertyGetter(AccessTools.TypeByName("RWF.LobbyCharacter"), "NickName");
            if (nickName != null)
            {
                harmony.Patch(nickName, postfix: new HarmonyMethod(AccessTools.Method(typeof(BotLook), nameof(BotName))));
            }
        }

        /// <summary>Номери (localID) ботів цього гравця, які він оголосив у кімнаті.</summary>
        public static int[] AnnouncedBots(PhotonPlayer owner)
        {
            if (owner != null && owner.IsLocal)
            {
                return OnlineBots.LocalBots;
            }

            return owner != null && owner.CustomProperties[BotsKey] is int[] ids ? ids : Array.Empty<int>();
        }

        public static bool IsBot(Player player)
        {
            if (player == null)
            {
                return false;
            }

            if (player.GetComponent<BotBrain>() != null)
            {
                return true;
            }

            if (PhotonNetwork.OfflineMode || AdditionalDataMethod == null || LocalIdField == null || player.data?.view == null)
            {
                return false;
            }

            object extra = AdditionalDataMethod.Invoke(null, new object[] { player });
            return extra != null && AnnouncedBots(player.data.view.Owner).Contains((int)LocalIdField.GetValue(extra));
        }

        public static void ApplyVanillaFace(CharacterCreatorItemEquipper equipper)
        {
            if (equipper != null)
            {
                equipper.EquipFace(VanillaFace);
            }
        }

        /// <summary>Чи цей персонаж лобі RWF (LobbyCharacter) — бот.</summary>
        private static bool IsLobbyBot(object lobbyCharacter)
        {
            if (lobbyCharacter == null)
            {
                return false;
            }

            var traverse = Traverse.Create(lobbyCharacter);
            return AnnouncedBots(traverse.Field("networkPlayer").GetValue<PhotonPlayer>()).Contains(traverse.Field("localID").GetValue<int>());
        }

        private static object LobbyCharacterOf(Component selector) => selector == null ? null : Traverse.Create(selector).Property("currentPlayer").GetValue();

        /// <summary>Портрети ботів у лобі — ванільні (раз на кожну картку; далі тримає префікс на RPCA_SetFace).</summary>
        public static void RefreshLobbyFaces()
        {
            if (SelectorType == null)
            {
                return;
            }

            foreach (Component selector in UnityEngine.Object.FindObjectsOfType(SelectorType).OfType<Component>())
            {
                if (!IsLobbyBot(LobbyCharacterOf(selector)))
                {
                    continue;
                }

                foreach (CharacterCreatorItemEquipper equipper in selector.GetComponentsInChildren<CharacterCreatorItemEquipper>(true))
                {
                    if (VanillaApplied.Add(equipper.GetInstanceID()))
                    {
                        ApplyVanillaFace(equipper);
                    }
                }
            }
        }

        private static void MakeVanilla(ref int eyeID, ref Vector2 eyeOffset, ref int mouthID, ref Vector2 mouthOffset, ref int detailID, ref Vector2 detailOffset, ref int detail2ID, ref Vector2 detail2Offset)
        {
            eyeID = mouthID = detailID = detail2ID = 0;
            eyeOffset = mouthOffset = detailOffset = detail2Offset = Vector2.zero;
        }

        private static void VanillaFaceForBots(Player __instance, ref int eyeID, ref Vector2 eyeOffset, ref int mouthID, ref Vector2 mouthOffset, ref int detailID, ref Vector2 detailOffset, ref int detail2ID, ref Vector2 detail2Offset)
        {
            if (IsBot(__instance))
            {
                MakeVanilla(ref eyeID, ref eyeOffset, ref mouthID, ref mouthOffset, ref detailID, ref detailOffset, ref detail2ID, ref detail2Offset);
            }
        }

        private static void VanillaPortraitForBots(CharacterCreatorItemEquipper __instance, ref int eyeID, ref Vector2 eyeOffset, ref int mouthID, ref Vector2 mouthOffset, ref int detailID, ref Vector2 detailOffset, ref int detail2ID, ref Vector2 detail2Offset)
        {
            Component selector = SelectorType == null ? null : __instance.GetComponentInParent(SelectorType);
            if (selector != null && IsLobbyBot(LobbyCharacterOf(selector)))
            {
                MakeVanilla(ref eyeID, ref eyeOffset, ref mouthID, ref mouthOffset, ref detailID, ref detailOffset, ref detail2ID, ref detail2Offset);
            }
        }

        private static void VanillaFaceWhenBotPicks(CardChoiceVisuals __instance, int pickerID)
        {
            Player picker = PlayerManager.instance?.players?.FirstOrDefault(p => p != null && p.playerID == pickerID);
            if (IsBot(picker))
            {
                ApplyVanillaFace(__instance.GetComponentInChildren<CharacterCreatorItemEquipper>());
            }
        }

        /// <summary>Ім'я бота в бою («Bot N») або null, якщо це не бот.</summary>
        public static string BotNameFor(Player player)
        {
            if (player == null || PhotonNetwork.OfflineMode || AdditionalDataMethod == null || LocalIdField == null || player.data?.view == null)
            {
                return null;
            }

            object extra = AdditionalDataMethod.Invoke(null, new object[] { player });
            if (extra == null)
            {
                return null;
            }

            PhotonPlayer owner = player.data.view.Owner;
            int index = Array.IndexOf(AnnouncedBots(owner).OrderBy(id => id).ToArray(), (int)LocalIdField.GetValue(extra));
            if (index < 0)
            {
                return null;
            }

            return owner.IsLocal ? $"Bot {index + 1}" : $"Bot {index + 1} ({owner.NickName})";
        }

        /// <summary>Підписи над ботами в бою — «Bot N» замість ніка власника ПК.</summary>
        public static void RefreshNameTags()
        {
            if (PlayerManager.instance == null)
            {
                return;
            }

            foreach (Player player in PlayerManager.instance.players)
            {
                string name = BotNameFor(player);
                if (name == null)
                {
                    continue;
                }

                foreach (PlayerName tag in player.GetComponentsInChildren<PlayerName>(true))
                {
                    TMPro.TextMeshProUGUI text = tag.GetComponentInParent<TMPro.TextMeshProUGUI>();
                    if (text != null && text.text != name)
                    {
                        text.text = name;
                    }
                }
            }
        }

        /// <summary>«Bot 1», «Bot 2»… для своїх; «Bot 1 (нік)» — для ботів інших гравців.</summary>
        private static void BotName(object __instance, ref string __result)
        {
            var traverse = Traverse.Create(__instance);
            var owner = traverse.Field("networkPlayer").GetValue<PhotonPlayer>();
            int localId = traverse.Field("localID").GetValue<int>();
            int[] bots = AnnouncedBots(owner).OrderBy(id => id).ToArray();
            int index = Array.IndexOf(bots, localId);
            if (index < 0)
            {
                // Людина на ПК з ботами: RWF дописує номер персонажа («Shidzenreku 5») — прибираємо його.
                string numbered = $"{owner.NickName} {localId + 1}";
                if (bots.Length > 0 && __result == numbered)
                {
                    __result = owner.NickName;
                }

                return;
            }

            __result = owner.IsLocal ? $"Bot {index + 1}" : $"Bot {index + 1} ({owner.NickName})";
        }
    }
}
