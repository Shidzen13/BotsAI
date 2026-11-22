using System;
using System.IO;

namespace RoundsBots
{
    /// <summary>
    /// Діагностика ботів у RoundsBotsRecordings\botlog.txt (дописується, не губиться при перезапуску гри,
    /// на відміну від LogOutput.log). Кожен рядок — з назвою карти, щоб аналізувати по картах.
    /// </summary>
    internal static class BotLog
    {
        public static void Write(string line)
        {
            Plugin.Log.LogInfo(line);
            try
            {
                Directory.CreateDirectory(InputRecorder.Folder);
                File.AppendAllText(
                    Path.Combine(InputRecorder.Folder, "botlog.txt"),
                    $"{DateTime.Now:HH:mm:ss} map={NavMap.CurrentMapName()} {line}{Environment.NewLine}");
            }
            catch (IOException ex)
            {
                Plugin.Log.LogWarning($"botlog write failed: {ex.Message}");
            }
        }
    }
}
