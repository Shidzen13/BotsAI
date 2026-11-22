using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RoundsBots.Logic;
using UnityEngine;

namespace RoundsBots.Patches
{
    /// <summary>
    /// Ванільний вибір картки чекає натискання стрибка від контролера гравця — бот його ніколи не натисне,
    /// і гра зависає. Тут, коли вибирає бот, ми оцінюємо картки, підсвічуємо обрану і через паузу беремо її.
    /// Працює для оригінальних і модових карток (усі вони в одній колоді CardChoice).
    /// </summary>
    [HarmonyPatch(typeof(CardChoice), "DoPlayerSelect")]
    internal static class CardChoiceBotPickPatch
    {
        private static readonly AccessTools.FieldRef<CardChoice, List<GameObject>> SpawnedCards =
            AccessTools.FieldRefAccess<CardChoice, List<GameObject>>("spawnedCards");

        private static readonly AccessTools.FieldRef<CardChoice, bool> IsPlaying =
            AccessTools.FieldRefAccess<CardChoice, bool>("isPlaying");

        private static readonly AccessTools.FieldRef<CardChoice, int> CurrentlySelectedCard =
            AccessTools.FieldRefAccess<CardChoice, int>("currentlySelectedCard");

        private static readonly AccessTools.FieldRef<CardChoice, PickerType> CurrentPickerType =
            AccessTools.FieldRefAccess<CardChoice, PickerType>("pickerType");

        private const float BrowseStepSeconds = 0.3f;

        private static bool armed;
        private static float armedAt;
        private static float browseUntil;
        private static int chosenIndex;
        private static float pickAt;
        private static List<GameObject> armedCards;
        private static int armedCardCount;

        private static bool Prefix(CardChoice __instance)
        {
            List<GameObject> cards = SpawnedCards(__instance);
            if (!IsBotPickReady(__instance, cards))
            {
                armed = false;
                LogHumanOffer(__instance, cards);
                return true;
            }

            if (!armed || !ReferenceEquals(armedCards, cards) || armedCardCount != cards.Count)
            {
                Arm(cards, GetPickers(__instance.pickrID, CurrentPickerType(__instance)));
            }

            // Бот, як людина, переглядає всі картки по черзі (кожна перевертається), потім зупиняється на обраній.
            // Ванільний код сам підсвічує й перевертає картку з індексом currentlySelectedCard.
            float now = Time.unscaledTime;
            CurrentlySelectedCard(__instance) = now < browseUntil
                ? Mathf.Clamp((int)((now - armedAt) / BrowseStepSeconds), 0, cards.Count - 1)
                : chosenIndex;
            if (now < pickAt)
            {
                return true;
            }

            GameObject card = cards[chosenIndex];
            armed = false;
            Plugin.Log.LogInfo($"Bot (picker {__instance.pickrID}) picks card: {card.GetComponent<CardInfo>()?.cardName}");
            __instance.Pick(card);
            __instance.pickrID = -1;
            return false;
        }

        private static List<GameObject> loggedHumanCards;
        private static int loggedHumanCount;

        /// <summary>
        /// Картки, які пропонують людині: індекс, назва, опис, стати й оцінка бота — у лог і human_cards.txt.
        /// Так AI-гравець (і людина) бачить усі картки одразу, а не перевертає по одній.
        /// </summary>
        private static void LogHumanOffer(CardChoice choice, List<GameObject> cards)
        {
            if (choice.pickrID == -1 || IsPlaying(choice) || cards == null || cards.Count == 0 || cards.Any(c => c == null)
                || (ReferenceEquals(loggedHumanCards, cards) && loggedHumanCount == cards.Count))
            {
                return;
            }

            loggedHumanCards = cards;
            loggedHumanCount = cards.Count;
            try
            {
                var text = new System.Text.StringBuilder();
                text.AppendLine($"picker={choice.pickrID} time={Time.unscaledTime:F1} cards={cards.Count} (індекс 0 — крайня ліва; D/стрілка вправо — наступна)");
                for (int i = 0; i < cards.Count; i++)
                {
                    CardInfo info = cards[i].GetComponent<CardInfo>();
                    Player[] pickers = GetPickers(choice.pickrID, CurrentPickerType(choice));
                    float score = ScoreCard(cards[i], CountCloseRangeCards(pickers), OwnedRoles(pickers), pickers);
                    string stats = info?.cardStats == null ? "" : string.Join("; ", info.cardStats
                        .Where(st => st != null)
                        .Select(st => $"{st.amount} {st.stat}"));
                    text.AppendLine($"{i}: {info?.cardName} [{info?.rarity}] mod={CardOrigin.Parse(cards[i].name)} score={score:0.00} | {info?.cardDestription} | {stats}");
                }

                Plugin.Log.LogInfo("Human offered cards:" + System.Environment.NewLine + text);
                string folder = InputRecorder.Folder;
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.AppendAllText(System.IO.Path.Combine(folder, "human_cards.txt"), text + System.Environment.NewLine);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"Human card log failed: {ex.Message}");
            }
        }

        private static bool IsBotPickReady(CardChoice choice, List<GameObject> cards)
        {
            if (choice.pickrID == -1 || IsPlaying(choice) || cards == null || cards.Count == 0 || cards.Any(c => c == null))
            {
                return false;
            }

            Player[] pickers = GetPickers(choice.pickrID, CurrentPickerType(choice));

            // Якщо в команді є хоч одна людина — вибирає людина.
            return pickers.Length > 0
                && pickers.All(BotBrain.IsBot)
                && pickers.Any(p => p.data.view.IsMine);
        }

        private static Player[] GetPickers(int pickerId, PickerType pickerType)
        {
            PlayerManager manager = PlayerManager.instance;
            if (pickerType == PickerType.Team)
            {
                return manager.GetPlayersInTeam(pickerId) ?? new Player[0];
            }

            return manager.players.Where(p => p != null && p.playerID == pickerId).ToArray();
        }

        /// <summary>Скільки «ближніх» карток уже мають гравці (для синергії з базою знань карток).</summary>
        private static int CountCloseRangeCards(IEnumerable<Player> players)
        {
            return players
                .Where(p => p != null && p.data != null && p.data.currentCards != null)
                .SelectMany(p => p.data.currentCards)
                .Count(c => c != null && CardKnowledge.TryGet(c.cardName, out CardTraits t) && t.CloseRange);
        }

        /// <summary>Ролі вже взятих карток (атака/захист/утиліта) — для балансу збірки.</summary>
        private static List<CardRole> OwnedRoles(IEnumerable<Player> players)
        {
            return players
                .Where(p => p != null && p.data != null && p.data.currentCards != null)
                .SelectMany(p => p.data.currentCards)
                .Where(c => c != null)
                .Select(c => CardScoring.RoleOf(FeaturesOf(c.gameObject)))
                .ToList();
        }

        /// <summary>Числа картки: з бази знань (там і ефекти з коду карток), інакше — з її компонентів.</summary>
        private static CardFeatures FeaturesOf(GameObject card)
        {
            string name = card.GetComponent<CardInfo>()?.cardName;
            return CardKnowledge.TryGet(name, out CardTraits traits) && traits.Features != null
                ? traits.Features
                : CardFeatureReader.Read(card);
        }

        /// <summary>Скільки копій цієї картки вже мають гравці.</summary>
        private static int OwnedCopies(IEnumerable<Player> players, string cardName)
        {
            return players
                .Where(p => p != null && p.data != null && p.data.currentCards != null)
                .SelectMany(p => p.data.currentCards)
                .Count(c => c != null && c.cardName == cardName);
        }

        /// <summary>Оцінка = числові ефекти + база знань (ризик, ближній бій) + баланс збірки + штраф за дублікати.</summary>
        private static float ScoreCard(GameObject card, int ownCloseRange, List<CardRole> owned, Player[] pickers)
        {
            CardFeatures features = FeaturesOf(card);
            string name = card.GetComponent<CardInfo>()?.cardName;
            return CardScoring.Score(features)
                + CardKnowledge.Adjustment(name, ownCloseRange, GameRules.SelfDamageEnabled)
                + CardKnowledge.OriginAdjustment(CardOrigin.Parse(card.name), name)
                + CardScoring.BalanceAdjustment(CardScoring.RoleOf(features), owned)
                + CardKnowledge.DuplicateAdjustment(name, OwnedCopies(pickers, name))
                + CardSynergy.Adjustment(name, OwnedCardNames(pickers));
        }

        private static IEnumerable<string> OwnedCardNames(Player[] pickers)
        {
            return pickers
                .Where(p => p != null && p.data != null && p.data.currentCards != null)
                .SelectMany(p => p.data.currentCards)
                .Where(c => c != null)
                .Select(c => c.cardName);
        }

        private static void Arm(List<GameObject> cards, Player[] pickers)
        {
            BotSettings settings = Plugin.Settings;
            int ownCloseRange = CountCloseRangeCards(pickers);
            List<CardRole> owned = OwnedRoles(pickers);
            float[] scores = cards.Select(c => ScoreCard(c, ownCloseRange, owned, pickers)).ToArray();
            float[] noise = cards.Select(_ => Random.value).ToArray();

            chosenIndex = Mathf.Clamp(CardScoring.ChooseIndex(scores, noise, settings.CardRandomness), 0, cards.Count - 1);

            armedAt = Time.unscaledTime;
            browseUntil = armedAt + cards.Count * BrowseStepSeconds;
            pickAt = browseUntil + Plugin.CardPickDelay.Value;

            string options = string.Join(", ", cards.Select((c, i) =>
                $"{(i == chosenIndex ? "*" : "")}{c.GetComponent<CardInfo>()?.cardName}={scores[i]:0.00}"));
            Plugin.Log.LogInfo($"Bot evaluates {cards.Count} cards: {options}");
            armedCards = cards;
            armedCardCount = cards.Count;
            armed = true;
        }
    }
}
