using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Записує бій у CSV для аналізу: для кожного гравця (людина і боти) — позицію, швидкість, здоров'я,
    /// ввід (напрям, приціл, стрибок, постріл, блок), контакт із землею/стіною, а для людини ще й сирі клавіші,
    /// для бота — його рішення (чи бачить ціль, чи є чиста траєкторія).
    /// F9 вмикає/вимикає запис; з AutoRecord запис стартує сам на початку кожного бою.
    /// </summary>
    internal sealed class InputRecorder : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F9;
        private const float SampleInterval = 1f / 30f;

        private static readonly KeyCode[] TrackedKeys =
        {
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Space, KeyCode.LeftShift, KeyCode.E, KeyCode.Q, KeyCode.R, KeyCode.F,
            KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
        };

        private static readonly string Header =
            "time,kind,player,team,x,y,vx,vy,hp,maxHp,dead,dirX,dirY,aimX,aimY,jumpPressed,jumpHeld,shoot,block,blockReady,"
            + "grounded,wallGrab,sinceGrounded,targetDx,targetDy,keysHeld,keysDown,botCanSee,botClearShot";

        private ConfigEntry<bool> autoRecord;
        private StreamWriter writer;
        private float sampleTimer;
        private float startedAt;
        private bool wasInBattle;
        private readonly StringBuilder keysDownSinceSample = new StringBuilder();
        private readonly System.Collections.Generic.Dictionary<Player, Vector2> lastPositions = new System.Collections.Generic.Dictionary<Player, Vector2>();
        private readonly System.Collections.Generic.Dictionary<Player, (bool jump, bool block)> pressedSinceSample = new System.Collections.Generic.Dictionary<Player, (bool, bool)>();

        public static string Folder => Path.Combine(Paths.BepInExRootPath, "RoundsBotsRecordings");

        public void Init(ConfigFile config)
        {
            autoRecord = config.Bind("Recording", "AutoRecord", true, "Автоматично записувати кожен бій у RoundsBotsRecordings (F9 — вручну).");
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                if (writer == null)
                {
                    StartRecording("manual");
                }
                else
                {
                    StopRecording();
                }
            }

            bool inBattle = PlayerManager.instance != null && PlayerManager.instance.players.Any(p => p != null && p.data != null && p.data.isPlaying);
            if (inBattle && !wasInBattle && writer == null && autoRecord != null && autoRecord.Value)
            {
                StartRecording("auto");
            }

            wasInBattle = inBattle;

            if (writer != null)
            {
                foreach (KeyCode key in TrackedKeys)
                {
                    if (Input.GetKeyDown(key))
                    {
                        keysDownSinceSample.Append(KeyName(key)).Append(' ');
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (writer == null || PlayerManager.instance == null)
            {
                return;
            }

            foreach (Player player in PlayerManager.instance.players.Where(p => p != null && p.data != null))
            {
                GeneralInput input = player.data.input;
                pressedSinceSample.TryGetValue(player, out (bool jump, bool block) pressed);
                pressedSinceSample[player] = (pressed.jump | input.jumpWasPressed, pressed.block | input.shieldWasPressed);
            }

            sampleTimer -= Time.deltaTime;
            if (sampleTimer > 0f)
            {
                return;
            }

            float elapsed = Mathf.Max(SampleInterval - sampleTimer, 0.0001f);
            sampleTimer = SampleInterval;

            bool anyPlaying = false;
            foreach (Player player in PlayerManager.instance.players.Where(p => p != null && p.data != null && p.data.isPlaying))
            {
                anyPlaying = true;
                WriteSample(player, elapsed);
            }

            if (anyPlaying)
            {
                keysDownSinceSample.Length = 0;
            }
        }

        private void WriteSample(Player player, float elapsed)
        {
            CharacterData data = player.data;
            GeneralInput input = data.input;
            Vector2 position = player.transform.position;
            Vector2 velocity = lastPositions.TryGetValue(player, out Vector2 last) ? (position - last) / elapsed : Vector2.zero;
            lastPositions[player] = position;
            pressedSinceSample.TryGetValue(player, out (bool jump, bool block) pressed);
            pressedSinceSample[player] = (false, false);

            Player enemy = PlayerManager.instance.players
                .Where(p => p != null && p != player && p.teamID != player.teamID && p.data != null && !p.data.dead)
                .OrderBy(p => ((Vector2)p.transform.position - position).sqrMagnitude)
                .FirstOrDefault();
            Vector2 toEnemy = enemy != null ? (Vector2)enemy.transform.position - position : Vector2.zero;

            BotBrain brain = player.GetComponent<BotBrain>();
            bool isHuman = brain == null && data.view.IsMine;
            string keysHeld = isHuman ? string.Join(" ", TrackedKeys.Where(Input.GetKey).Select(KeyName)) : "";
            string keysDown = isHuman ? keysDownSinceSample.ToString().Trim() : "";

            string line = string.Join(",", new[]
            {
                F(Time.time - startedAt), brain != null ? "bot" : "human", I(player.playerID), I(player.teamID),
                F(position.x), F(position.y), F(velocity.x), F(velocity.y),
                F(data.health), F(data.maxHealth), B(data.dead),
                F(input.direction.x), F(input.direction.y), F(input.aimDirection.x), F(input.aimDirection.y),
                B(pressed.jump), B(input.jumpIsPressed), B(input.shootIsPressed), B(pressed.block),
                B(data.block != null && !data.block.IsOnCD()),
                B(data.isGrounded), B(data.isWallGrab), F(data.sinceGrounded),
                F(toEnemy.x), F(toEnemy.y),
                keysHeld, keysDown,
                brain != null ? B(brain.LastCanSee) : "", brain != null ? B(brain.LastClearShot) : "",
            });
            writer.WriteLine(line);
        }

        private void StartRecording(string reason)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, $"rec_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                writer = new StreamWriter(path, false) { AutoFlush = true };
                writer.WriteLine(Header);
                startedAt = Time.time;
                sampleTimer = 0f;
                lastPositions.Clear();
                pressedSinceSample.Clear();
                keysDownSinceSample.Length = 0;
                Plugin.Log.LogInfo($"Recording started ({reason}): {path}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Could not start recording: {ex}");
                writer = null;
            }
        }

        private void StopRecording()
        {
            writer.Dispose();
            writer = null;
            Plugin.Log.LogInfo("Recording stopped.");
        }

        private void OnDestroy()
        {
            writer?.Dispose();
        }

        private static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Mouse0: return "LMB";
                case KeyCode.Mouse1: return "RMB";
                case KeyCode.Mouse2: return "MMB";
                case KeyCode.LeftShift: return "Shift";
                default: return key.ToString();
            }
        }

        private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string B(bool value) => value ? "1" : "0";
    }
}
