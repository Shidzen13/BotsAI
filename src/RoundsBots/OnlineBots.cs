using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using InControl;
using Photon.Pun;
using RoundsBots.Logic;
using UnboundLib.GameModes;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Боти в онлайн-кімнаті RoundsWithFriends. У кімнаті B додає бота, N прибирає останнього.
    /// Бот — ще один «локальний» персонаж того, хто його додав (як другий геймпад), тому RWF сам створює його
    /// в мережі; керує ним BotBrain лише на цьому ПК, решта бачать звичайну синхронізацію Photon.
    /// RWF — м'яка залежність, тож усе через рефлексію.
    /// </summary>
    internal sealed class OnlineBots : MonoBehaviour
    {
        private const string BotDeviceName = "RoundsBot";
        private const string PlayersKey = "players";
        private const float ReReadySeconds = 0.5f;

        private static readonly Type HandlerType = AccessTools.TypeByName("RWF.PrivateRoomHandler");
        private static readonly Type CharacterType = AccessTools.TypeByName("RWF.LobbyCharacter");
        private static readonly Type ModType = AccessTools.TypeByName("RWF.RWFMod");
        private static readonly Type PlayerExtensionsType = AccessTools.TypeByName("RWF.PhotonPlayerExtensions");

        private static readonly FieldInfo InstanceField = AccessTools.Field(HandlerType, "instance");
        private static readonly PropertyInfo IsOpenProperty = AccessTools.Property(HandlerType, "IsOpen");
        private static readonly PropertyInfo CharactersProperty = AccessTools.Property(HandlerType, "PrivateRoomCharacters");
        private static readonly PropertyInfo LockReadiesProperty = AccessTools.Property(HandlerType, "lockReadies");
        private static readonly FieldInfo DevicesField = AccessTools.Field(HandlerType, "devicesToUse");
        private static readonly MethodInfo RemovePlayerMethod = AccessTools.Method(HandlerType, "RemovePlayer");
        private static readonly PropertyInfo ColorLimitProperty = AccessTools.Property(ModType, "MaxColorsHardLimit");
        private static readonly FieldInfo ReadyField = AccessTools.Field(CharacterType, "ready");
        private static readonly FieldInfo ColorField = AccessTools.Field(CharacterType, "colorID");
        private static readonly MethodInfo SetPropertyMethod = AccessTools.Method(PlayerExtensionsType, "SetProperty");
        private static readonly MethodInfo GetCharactersMethod = MakeGetCharacters();
        private static readonly MethodInfo AdditionalDataMethod = AccessTools.Method(AccessTools.TypeByName("RWF.PlayerExtensions"), "GetAdditionalData");
        private static readonly FieldInfo AssignedCharacterField = AccessTools.Field(AccessTools.TypeByName("RWF.PlayerAdditionalData"), "character");

        private static readonly PropertyInfo UniqueIdProperty = AccessTools.Property(CharacterType, "uniqueID");
        private static readonly Type VersusType = AccessTools.TypeByName("RWF.VersusDisplay");
        private static readonly FieldInfo VersusInstanceField = AccessTools.Field(VersusType, "instance");
        private static readonly MethodInfo SelectorMethod = AccessTools.Method(VersusType, "PlayerSelectorGO");
        private const float MessageSeconds = 2f;
        private const float SelectorRepairSeconds = 1f;

        /// <summary>
        /// Слоти своїх ботів — відомі одразу, без очікування мережі (ім'я «Bot N» береться в момент створення картки в лобі).
        /// </summary>
        public static int[] LocalBots { get; private set; } = new int[0];

        private const float AddedGraceSeconds = 3f;
        private static readonly Dictionary<int, float> AddedAt = new Dictionary<int, float>();

        private static IEnumerable<int> RecentlyAdded() =>
            AddedAt.Where(kv => Time.unscaledTime - kv.Value < AddedGraceSeconds).Select(kv => kv.Key).ToArray();
        private static readonly FieldInfo SelectorsField = AccessTools.Field(VersusType, "_playerSelectorGOs");
        private static readonly FieldInfo SelectorsCreatedField = AccessTools.Field(VersusType, "_playerSelectorGOsCreated");
        private static readonly Dictionary<int, float> MissingSelectorSince = new Dictionary<int, float>();
        private static int[] announcedBots = new int[0];
        private const int MaxBots = 4;

        private static readonly HashSet<InputDevice> BotDevices = new HashSet<InputDevice>();

        /// <summary>Коротке повідомлення після дії (показує <see cref="BotsOverlay"/>).</summary>
        public static string Message { get; private set; }

        public static float MessageUntil { get; private set; }

        /// <summary>Кімната RWF відкрита (для підказки на екрані).</summary>
        public static bool RoomOpen { get; private set; }

        private static void Say(string text)
        {
            Message = text;
            MessageUntil = Time.unscaledTime + MessageSeconds;
            BotLog.Write($"ONLINE {text}");
        }

        private float nextReReady;

        private static MethodInfo MakeGetCharacters()
        {
            MethodInfo generic = PlayerExtensionsType == null || CharacterType == null ? null : AccessTools.Method(PlayerExtensionsType, "GetProperty");
            return generic == null ? null : generic.MakeGenericMethod(CharacterType.MakeArrayType());
        }

        /// <summary>RWF уже призначив гравцю персонажа (playerID, команду) — раніше мозок вмикати не можна.</summary>
        private static bool IsAssigned(Player player)
        {
            if (AdditionalDataMethod == null || AssignedCharacterField == null)
            {
                return true;
            }

            object extra = AdditionalDataMethod.Invoke(null, new object[] { player });
            return extra != null && AssignedCharacterField.GetValue(extra) != null;
        }

        public static bool Available =>
            InstanceField != null && IsOpenProperty != null && CharactersProperty != null && LockReadiesProperty != null
            && DevicesField != null && RemovePlayerMethod != null && ColorLimitProperty != null && ReadyField != null
            && ColorField != null && SetPropertyMethod != null && GetCharactersMethod != null;

        private float nextNameRefresh;

        private void Update()
        {
            AttachBrains();
            if (Time.unscaledTime >= nextNameRefresh && !PhotonNetwork.OfflineMode && PhotonNetwork.CurrentRoom != null)
            {
                nextNameRefresh = Time.unscaledTime + ReReadySeconds;
                BotLook.RefreshNameTags();
            }

            object handler = OpenRoom();
            RoomOpen = handler != null;
            if (handler == null)
            {
                OnlineSolo.OwnBotsInRoom = 0;
                if (PhotonNetwork.CurrentRoom == null)
                {
                    LocalBots = new int[0];
                }

                return;
            }

            if (IsLocked(handler))
            {
                return;
            }

            try
            {
                OnlineSolo.Announce();
                LocalBots = BotSlots(handler).Concat(RecentlyAdded()).Distinct().OrderBy(s => s).ToArray();
                OnlineSolo.OwnBotsInRoom = LocalBots.Length;
                AnnounceBots(handler);
                EnsureFaceSlots();
                BotsSummary = DescribeBots(handler);
                if (!GameManager.lockInput && !DevConsole.isTyping)
                {
                    if (Input.GetKeyDown(KeyCode.B))
                    {
                        if (OnlineSolo.PeersCompatible(out string problem))
                        {
                            AddBot(handler);
                        }
                        else
                        {
                            Say(problem);
                        }
                    }
                    else if (Input.GetKeyDown(KeyCode.N))
                    {
                        RemoveBot(handler);
                    }
                    else
                    {
                        HandleColorKeys(handler);
                    }
                }

                if (Time.unscaledTime >= nextReReady)
                {
                    nextReReady = Time.unscaledTime + ReReadySeconds;
                    KeepBotsReady(handler);
                    RepairSelectors(handler);
                    BotLook.RefreshLobbyFaces();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Online bots: {ex}");
            }
        }

        /// <summary>Гравець, створений RWF з «пристроєм-ботом», стає ботом (лише на ПК власника).</summary>
        private static void AttachBrains()
        {
            if (BotDevices.Count == 0 || PlayerManager.instance == null)
            {
                return;
            }

            foreach (Player player in PlayerManager.instance.players)
            {
                CharacterData data = player != null ? player.data : null;
                if (data == null || data.view == null || !data.view.IsMine || data.playerActions == null
                    || !BotDevices.Contains(data.playerActions.Device) || player.GetComponent<BotBrain>() != null
                    || !IsAssigned(player))
                {
                    continue;
                }

                data.SetAI(); // CharacterDataSetAiPatch вимикає PlayerAPI і додає BotBrain
                BotLog.Write($"ONLINE bot spawned player={player.playerID} team={player.teamID}");
            }
        }

        private static object OpenRoom()
        {
            if (PhotonNetwork.OfflineMode || PhotonNetwork.CurrentRoom == null)
            {
                return null;
            }

            object handler = InstanceField.GetValue(null);
            return handler != null && (bool)IsOpenProperty.GetValue(handler) ? handler : null;
        }

        private static bool IsLocked(object handler) => (bool)LockReadiesProperty.GetValue(handler);

        private static Array LocalCharacters()
        {
            return (Array)GetCharactersMethod.Invoke(null, new object[] { PhotonNetwork.LocalPlayer, PlayersKey });
        }

        private static void SaveLocalCharacters(Array characters)
        {
            SetPropertyMethod.Invoke(null, new object[] { PhotonNetwork.LocalPlayer, PlayersKey, characters });
        }

        private static IDictionary<int, InputDevice> Devices(object handler) => (IDictionary<int, InputDevice>)DevicesField.GetValue(handler);

        private static void AddBot(object handler)
        {
            Array current = LocalCharacters();
            if (current == null)
            {
                return;
            }

            if (BotSlots(handler).Count() >= MaxBots)
            {
                Say($"max {MaxBots} bots");
                return;
            }

            bool[] occupied = current.Cast<object>().Select(c => c != null).ToArray();
            int slot = LobbySlots.FirstFreeSlot(occupied);
            if (slot < 0)
            {
                Say($"no free slot: {current.Length} characters per PC (config [Online] MaxCharactersPerClient)");
                return;
            }

            var usedColors = ((Array)CharactersProperty.GetValue(handler)).Cast<object>().Select(c => (int)ColorField.GetValue(c));
            int color = LobbySlots.FirstFreeColor(usedColors, (int)ColorLimitProperty.GetValue(null));

            var device = new InputDevice(BotDeviceName);
            BotDevices.Add(device);
            Devices(handler)[slot] = device; // до SetProperty: інтерфейс персонажа чекає на пристрій

            LocalBots = LocalBots.Concat(new[] { slot }).Distinct().OrderBy(s => s).ToArray();
            AddedAt[slot] = Time.unscaledTime;
            object bot = Activator.CreateInstance(CharacterType, PhotonNetwork.LocalPlayer, color, slot);
            ReadyField.SetValue(bot, true);
            Array updated = (Array)current.Clone();
            updated.SetValue(bot, slot);
            SaveLocalCharacters(updated);
            BotLog.Write($"ONLINE bot added slot={slot} color={color}");
        }

        private static void RemoveBot(object handler)
        {
            Array current = LocalCharacters();
            IDictionary<int, InputDevice> devices = Devices(handler);
            if (current == null)
            {
                return;
            }

            for (int slot = current.Length - 1; slot >= 0; slot--)
            {
                object character = current.GetValue(slot);
                if (character != null && devices.TryGetValue(slot, out InputDevice device) && BotDevices.Contains(device))
                {
                    var routine = (IEnumerator)RemovePlayerMethod.Invoke(handler, new[] { character });
                    ((MonoBehaviour)handler).StartCoroutine(routine);
                    BotDevices.Remove(device);
                    LocalBots = LocalBots.Where(s => s != slot).ToArray();
                    AddedAt.Remove(slot);
                    BotLog.Write($"ONLINE bot removed slot={slot}");
                    return;
                }
            }
        }

        /// <summary>Слоти наших ботів серед локальних персонажів (за «пристроєм-ботом»).</summary>
        private static IEnumerable<int> BotSlots(object handler)
        {
            Array current = LocalCharacters();
            IDictionary<int, InputDevice> devices = Devices(handler);
            if (current == null)
            {
                yield break;
            }

            for (int slot = 0; slot < current.Length; slot++)
            {
                if (current.GetValue(slot) != null && devices.TryGetValue(slot, out InputDevice device) && BotDevices.Contains(device))
                {
                    yield return slot;
                }
            }
        }

        private static readonly KeyCode[] DigitKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };
        private static readonly KeyCode[] KeypadKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4 };

        /// <summary>Список ботів для підказки: «1: колір 3   2: колір 5».</summary>
        public static string BotsSummary { get; private set; } = string.Empty;

        /// <summary>1..4 — наступний колір боту №N; Shift+цифра — попередній колір.</summary>
        private static void HandleColorKeys(object handler)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            for (int i = 0; i < DigitKeys.Length; i++)
            {
                if (Input.GetKeyDown(DigitKeys[i]) || Input.GetKeyDown(KeypadKeys[i]))
                {
                    RecolorBot(handler, i, shift ? -1 : 1);
                    return;
                }
            }
        }

        private static string DescribeBots(object handler)
        {
            Array current = LocalCharacters();
            if (current == null)
            {
                return string.Empty;
            }

            return string.Join("   ", BotSlots(handler).Select((slot, index) => $"{index + 1}: color {(int)ColorField.GetValue(current.GetValue(slot)) + 1}"));
        }

        /// <summary>
        /// Колір = команда в RWF; зміна через штатний RPC RPCA_ChangeTeam (як HandleTeamRules).
        /// Команди тримаються лише в режимах з командами.
        /// </summary>
        private static void RecolorBot(object handler, int botIndex, int step)
        {
            Array current = LocalCharacters();
            int[] slots = BotSlots(handler).ToArray();
            if (current == null || botIndex >= slots.Length)
            {
                Say($"no bot #{botIndex + 1} - press B to add");
                return;
            }

            object bot = current.GetValue(slots[botIndex]);
            int colorCount = (int)ColorLimitProperty.GetValue(null);
            int count = Math.Max(1, colorCount);
            int newColor = (((int)ColorField.GetValue(bot) + step) % count + count) % count;

            int botId = (int)UniqueIdProperty.GetValue(bot);
            var colors = ((Array)CharactersProperty.GetValue(handler)).Cast<object>()
                .Select(c => (int)UniqueIdProperty.GetValue(c) == botId ? newColor : (int)ColorField.GetValue(c));
            if (colors.Distinct().Count() < 2)
            {
                Say("need at least 2 colors, otherwise the game won't start");
                return;
            }

            if (GameModeManager.CurrentHandler != null && !GameModeManager.CurrentHandler.AllowTeams)
            {
                Say("teams work only in Team Deathmatch");
                return;
            }

            object versus = VersusInstanceField?.GetValue(null);
            var selector = versus != null ? SelectorMethod?.Invoke(versus, new object[] { botId }) as GameObject : null;
            PhotonView view = selector != null ? selector.GetComponent<PhotonView>() : null;
            if (view == null)
            {
                Say("bot not found in the lobby yet - try again in a second");
                return;
            }

            view.RPC("RPCA_ChangeTeam", RpcTarget.All, newColor);
            Say($"bot #{botIndex + 1}: color {newColor + 1}");
        }

        /// <summary>Оголосити в кімнаті номери своїх ботів (ім'я «Bot N» і ванільне обличчя бачать усі з BotsAI).</summary>
        private static void AnnounceBots(object handler)
        {
            int[] bots = BotSlots(handler).ToArray();
            if (bots.SequenceEqual(announcedBots))
            {
                return;
            }

            announcedBots = bots;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [BotLook.BotsKey] = bots });
        }

        /// <summary>Ванільний масив облич — на 4 гравців; RWF бере обличчя за localID, а наш 5-й персонаж має localID 4.</summary>
        private static void EnsureFaceSlots()
        {
            CharacterCreatorHandler creator = CharacterCreatorHandler.instance;
            int needed = OnlineSolo.ConfiguredLimit;
            if (creator == null || creator.selectedPlayerFaces == null || creator.selectedPlayerFaces.Length >= needed)
            {
                return;
            }

            PlayerFace[] faces = new PlayerFace[needed];
            for (int i = 0; i < needed; i++)
            {
                faces[i] = i < creator.selectedPlayerFaces.Length ? creator.selectedPlayerFaces[i] : creator.selectedPlayerFaces[0];
            }

            creator.selectedPlayerFaces = faces;
        }

        /// <summary>
        /// RWF іноді «забуває» картку бота в лобі: її номер лишається в списку створених, а самої картки немає
        /// (бота прибрали/додали, поки картка ще створювалась) — тоді RWF більше її не створює. Через 3 с знімаємо позначку.
        /// </summary>
        private static void RepairSelectors(object handler)
        {
            object versus = VersusInstanceField?.GetValue(null);
            var selectors = versus == null ? null : SelectorsField?.GetValue(versus) as IDictionary<int, GameObject>;
            var created = versus == null ? null : SelectorsCreatedField?.GetValue(versus) as List<int>;
            Array current = LocalCharacters();
            if (selectors == null || created == null || current == null)
            {
                return;
            }

            foreach (int slot in BotSlots(handler).ToArray())
            {
                int id = (int)UniqueIdProperty.GetValue(current.GetValue(slot));
                bool stuck = created.Contains(id) && (!selectors.TryGetValue(id, out GameObject go) || go == null);
                if (!stuck)
                {
                    MissingSelectorSince.Remove(id);
                    continue;
                }

                if (!MissingSelectorSince.TryGetValue(id, out float since))
                {
                    MissingSelectorSince[id] = Time.unscaledTime;
                }
                else if (Time.unscaledTime - since >= SelectorRepairSeconds)
                {
                    created.Remove(id);
                    MissingSelectorSince.Remove(id);
                    BotLog.Write($"ONLINE repaired lobby card slot={slot}");
                }
            }
        }

        /// <summary>RWF скидає «готовність» усім (зміна налаштувань тощо) — бот одразу знову готовий.</summary>
        private static void KeepBotsReady(object handler)
        {
            Array current = LocalCharacters();
            IDictionary<int, InputDevice> devices = Devices(handler);
            if (current == null)
            {
                return;
            }

            Array updated = null;
            for (int slot = 0; slot < current.Length; slot++)
            {
                object character = current.GetValue(slot);
                if (character == null || (bool)ReadyField.GetValue(character)
                    || !devices.TryGetValue(slot, out InputDevice device) || !BotDevices.Contains(device))
                {
                    continue;
                }

                updated = updated ?? (Array)current.Clone();
                ReadyField.SetValue(character, true);
            }

            if (updated != null)
            {
                SaveLocalCharacters(updated);
            }
        }
    }
}
