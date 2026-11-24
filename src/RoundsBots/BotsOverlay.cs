using Photon.Pun;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Підказка з клавішами на екрані: в онлайн-кімнаті RWF (B/N/1–4 + попередження) і в LOCAL-лобі (B).
    /// Лише в лобі — під час бою не заважає.
    /// </summary>
    internal sealed class BotsOverlay : MonoBehaviour
    {
        private const float LobbyCheckInterval = 1f;
        private const int Width = 380;

        private GUIStyle box;
        private float nextLobbyCheckAt;
        private bool localLobby;

        private void Update()
        {
            if (Time.unscaledTime < nextLobbyCheckAt)
            {
                return;
            }

            nextLobbyCheckAt = Time.unscaledTime + LobbyCheckInterval;
            localLobby = PhotonNetwork.OfflineMode && FindObjectOfType<CharacterSelectionInstance>() != null;
        }

        private void OnGUI()
        {
            string text = OnlineBots.RoomOpen ? RoomText() : localLobby ? "BotsAI: B - add bot" : null;
            bool showMessage = OnlineBots.Message != null && Time.unscaledTime < OnlineBots.MessageUntil;
            if (text == null && !showMessage)
            {
                return;
            }

            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, wordWrap = true, richText = true };
            }

            string full = text ?? string.Empty;
            if (showMessage)
            {
                full += (full.Length > 0 ? "\n\n" : string.Empty) + $"<color=#ffd27f>{OnlineBots.Message}</color>";
            }

            float height = box.CalcHeight(new GUIContent(full), Width) + 8f;
            GUI.Box(new Rect(12f, Screen.height - height - 12f, Width, height), full, box);
        }

        private static string RoomText()
        {
            string bots = OnlineBots.BotsSummary.Length > 0 ? $"\n<b>Bots:</b> {OnlineBots.BotsSummary}" : string.Empty;
            int maxBots = Mathf.Max(0, OnlineSolo.ConfiguredLimit - 1);
            string text = $"<b>BotsAI</b>\nB - add bot (Bots: {OnlineBots.LocalBots.Length}/{maxBots})   N - remove bot\n" +
                "1-4 - next color for bot #N   Shift+1-4 - previous color" + bots + "\n" +
                $"<size=12>Teams only in Team Deathmatch. Characters per PC: {OnlineSolo.ConfiguredLimit}</size>";
            if (!OnlineSolo.PeersCompatible(out string problem))
            {
                text += $"\n<color=#ff6b6b>{problem}</color>";
            }

            if (OnlineSolo.SoloUnavailableReason != null)
            {
                text += $"\n<color=#ff6b6b>{OnlineSolo.SoloUnavailableReason}</color>";
            }

            return text;
        }
    }
}
