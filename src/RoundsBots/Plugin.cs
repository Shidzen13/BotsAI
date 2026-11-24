using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RoundsBots.Logic;

namespace RoundsBots
{
    [BepInPlugin(ModId, ModName, ModVersion)]
    [BepInDependency("com.willis.rounds.unbound", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("io.olavim.rounds.rwf", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess("Rounds.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string ModId = "com.kanty.rounds.bots";
        public const string ModName = "BotsAI";
        public const string ModVersion = "0.5.4";

        internal static ManualLogSource Log { get; private set; }

        internal static Plugin Instance { get; private set; }

        internal static ConfigEntry<BotDifficulty> Difficulty { get; private set; }

        internal static ConfigEntry<float> CardPickDelay { get; private set; }

        internal static ConfigEntry<BotExperiment> Experiment { get; private set; }

        internal static ConfigEntry<bool> AutoRematch { get; private set; }

        internal static ConfigEntry<float> ForceEndSeconds { get; private set; }

        internal static ConfigEntry<int> MaxCharactersPerClient { get; private set; }

        internal static BotSettings Settings { get; private set; } = BotSettings.For(BotDifficulty.Normal);

        /// <summary>Після завантаження всіх модів: список у журнал (боти й аналіз знають, з якими модами грають).</summary>
        private void Start()
        {
            try
            {
                DamageTracker.RegisterHooks();
                TransitionDiagnostics.RegisterHooks();
                UnboundLib.GameModes.GameModeManager.AddHook(UnboundLib.GameModes.GameModeHooks.HookPointStart, handler => NavMap.OnPointStart());
            }
            catch (System.Exception ex)
            {
                Log.LogWarning($"Game hooks failed: {ex.Message}");
            }

            try
            {
                // Hidden Cards вантажиться після нас — патчимо, коли всі моди вже в пам'яті.
                HiddenCardsCompat.Apply(new Harmony(ModId + ".compat"));
                InfoholicCompat.Apply(new Harmony(ModId + ".compat.infoholic"));
            }
            catch (System.Exception ex)
            {
                Log.LogWarning($"Hidden Cards compat failed: {ex.Message}");
            }

            try
            {
                BotsMenu.Register();
            }
            catch (System.Exception ex)
            {
                Log.LogWarning($"Mod options menu failed: {ex.Message}");
            }

            try
            {
                var mods = BepInEx.Bootstrap.Chainloader.PluginInfos.Values
                    .Select(info => $"{info.Metadata.Name} {info.Metadata.Version}")
                    .OrderBy(name => name)
                    .ToArray();
                string line = $"MODS count={mods.Length} selfDamage={(GameRules.SelfDamageEnabled ? "on" : "off")}: {string.Join(", ", mods)}";
                Log.LogInfo(line);
                BotLog.Write(line);
            }
            catch (System.Exception ex)
            {
                Log.LogWarning($"Mod list failed: {ex.Message}");
            }
        }

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            Difficulty = Config.Bind(
                "Bots",
                "Difficulty",
                BotDifficulty.Normal,
                "Складність ботів: Easy, Normal, Hard.");

            CardPickDelay = Config.Bind(
                "Bots",
                "CardPickDelay",
                1.0f,
                new ConfigDescription(
                    "Скільки секунд бот «думає» перед вибором картки.",
                    new AcceptableValueRange<float>(0.1f, 5f)));

            Experiment = Config.Bind(
                "Experiment",
                "DisableForOddBots",
                BotExperiment.None,
                "A/B: боти з непарним id вимикають цю функцію. Результати — рядки RESULT у LogOutput.log.");

            AutoRematch = Config.Bind(
                "Experiment",
                "AutoRematch",
                true,
                "Якщо грають лише боти — наприкінці матчу автоматично REMATCH (безперервні експерименти).");

            ForceEndSeconds = Config.Bind(
                "Bots",
                "ForceEndPointSeconds",
                Stalemate.ForceEndSeconds,
                new ConfigDescription(
                    "Якщо живі лише твої боти і стільки секунд ніхто не отримав шкоди — найслабший бот гине, очко закінчується. 0 — вимкнено.",
                    new AcceptableValueRange<float>(0f, 600f)));

            MaxCharactersPerClient = Config.Bind(
                "Online",
                "MaxCharactersPerClient",
                2,
                new ConfigDescription(
                    "Скільки персонажів (ти + боти) з одного ПК в онлайн-кімнаті RWF. 2 — як у RWF, безпечно з другом без мода. " +
                    "5 (ти + 4 боти) — лише для гри самому або коли в друга BotsAI з тим самим значенням. Міняти до входу в кімнату.",
                    new AcceptableValueRange<int>(2, 5)));

            Settings = BotSettings.For(Difficulty.Value);
            Difficulty.SettingChanged += (_, __) => Settings = BotSettings.For(Difficulty.Value);

            var harmony = new Harmony(ModId);
            harmony.PatchAll(typeof(Plugin).Assembly);
            gameObject.AddComponent<InputRecorder>().Init(Config);
            gameObject.AddComponent<PointWatchdog>();
            if (OnlineBots.Available)
            {
                gameObject.AddComponent<OnlineBots>();
                try
                {
                    OnlineSolo.Apply(harmony);
                    BotLook.Apply(harmony);
                }
                catch (System.Exception ex)
                {
                    Log.LogWarning($"Online solo patch failed: {ex.Message}");
                }
            }
            else
            {
                Log.LogInfo("RoundsWithFriends not found: online bots disabled.");
            }

            gameObject.AddComponent<BotsOverlay>();
            Log.LogInfo($"{ModName} {ModVersion} loaded. Difficulty={Difficulty.Value}. Press B in the LOCAL lobby or an online RWF room to add a bot (N removes one online).");
        }
    }
}
