using RoundsBots.Logic;
using UnityEngine;

namespace RoundsBots
{
    /// <summary>
    /// Сканує всю арену (x ±36, y ±20) у сітку 1×1 через Physics2D і будує <see cref="NavGrid"/>.
    /// Спільна для всіх ботів. Перебудовується на початку кожного очка (<see cref="Invalidate"/> — нова карта)
    /// і про всяк випадок раз на <see cref="RebuildInterval"/> с. Пилки позначаються як перешкоди.
    /// Рухомі ящики й кулі (вільні Rigidbody2D) теж тверді — на них стрибають, а не пробігають крізь них;
    /// поки вони є на карті, сітка оновлюється частіше (<see cref="MovableRebuildInterval"/>), бо їх штовхають кулями.
    /// </summary>
    internal static class NavMap
    {
        public const float CellSize = 1f;
        private const int PlayerClearance = 1; // гравець ~1 одиниця: на реальних картах так зв'язність вища (тест RealMapTests)
        private const float RebuildInterval = 10f;
        private const float MovableRebuildInterval = 2f;
        private const float MovingPartsRebuildInterval = 1f;
        private static bool hasMovingParts;

        // Ідея з RoundsWithBots (він пересканує мапу щокадру): рухомі платформи (кінематичні тіла), анімовані об'єкти
        // MapsExtended і руйнівні перегородки теж міняють прохідність — для таких карт скануємо частіше.
        private static System.Type mapAnimationType;
        private static bool mapAnimationTypeLooked;
        private const float MinRebuildGap = 1f;
        private const float OriginX = -36f;
        private const float OriginY = -20f;
        private const int Width = 72;
        private const int Height = 40;
        private static readonly Vector2 ProbeSize = new Vector2(0.9f, 0.9f);
        private static readonly Collider2D[] Buffer = new Collider2D[16];

        private static NavGrid grid;
        private static float builtAt = float.NegativeInfinity;
        private static MapWrapper builtFor;
        private static MapWrapper dumpedFor;
        private static bool hasMovables;

        /// <summary>Вивчені «погані» переходи по картах (<see cref="EdgeLearning"/>): лише справжні падіння, зі згасанням.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, EdgeLearning> Learned =
            new System.Collections.Generic.Dictionary<string, EdgeLearning>();

        /// <summary>Номер очка/карти: спроба кроку з минулого очка не оцінюється (смерть, вибір карток, нова мапа).</summary>
        public static int Generation { get; private set; }

        private static EdgeLearning LearningForCurrentMap()
        {
            string map = CurrentMapName() ?? "unknown";
            if (!Learned.TryGetValue(map, out var learning))
            {
                learning = new EdgeLearning();
                Learned[map] = learning;
            }

            return learning;
        }

        /// <summary>Перехід from→to закінчився падінням — штраф на цій карті (і в поточній сітці одразу).</summary>
        public static void ReportFailure(int fromX, int fromY, int toX, int toY, string move)
        {
            EdgeLearning learning = LearningForCurrentMap();
            foreach ((int fx, int fy, int tx, int ty) in learning.RecordFailure(fromX, fromY, toX, toY))
            {
                if (IsInside(fx, fy) && IsInside(tx, ty))
                {
                    grid?.SetPenalty(fx, fy, tx, ty, learning.PenaltyOf(fx, fy, tx, ty));
                }
            }

            BotLog.Write($"LEARN fail {move} ({fromX},{fromY})->({toX},{toY}) penalty={learning.PenaltyOf(fromX, fromY, toX, toY):0.#} edges={learning.Count}");
        }

        /// <summary>Перехід удався — знімаємо його штраф.</summary>
        public static void ReportSuccess(int fromX, int fromY, int toX, int toY)
        {
            if (Learned.TryGetValue(CurrentMapName() ?? "unknown", out var learning) && learning.RecordSuccess(fromX, fromY, toX, toY))
            {
                grid?.SetPenalty(fromX, fromY, toX, toY, 0f);
                BotLog.Write($"LEARN ok ({fromX},{fromY})->({toX},{toY}) penalty cleared");
            }
        }

        /// <summary>Нове очко: штрафи згасають (×0.5), старі спроби більше не оцінюються.</summary>
        public static System.Collections.IEnumerator OnPointStart()
        {
            Generation++;
            foreach (EdgeLearning learning in Learned.Values)
            {
                learning.Decay();
            }

            grid = null;
            yield break;
        }

        private static bool IsInside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        private static void ApplyLearned(NavGrid built)
        {
            if (Learned.TryGetValue(CurrentMapName() ?? "unknown", out var learning))
            {
                foreach (var entry in learning.All)
                {
                    (int fx, int fy, int tx, int ty) = entry.Key;
                    if (IsInside(fx, fy) && IsInside(tx, ty))
                    {
                        built.SetPenalty(fx, fy, tx, ty, entry.Value);
                    }
                }
            }
        }

        public static NavGrid Get()
        {
            float now = Time.time;
            MapWrapper map = MapManager.instance != null ? MapManager.instance.currentMap : null;
            float interval = hasMovingParts ? MovingPartsRebuildInterval : hasMovables ? MovableRebuildInterval : RebuildInterval;
            if (grid == null || now - builtAt >= interval || now < builtAt || !ReferenceEquals(map, builtFor))
            {
                builtFor = map;
                grid = Build();
                builtAt = now;
            }

            return grid;
        }

        /// <summary>Нове очко/карта — наступний <see cref="Get"/> пересканує арену.</summary>
        public static void Invalidate()
        {
            if (Time.time - builtAt >= MinRebuildGap)
            {
                grid = null;
            }
        }

        public static (int x, int y) ToCell(Vector2 world)
        {
            return (Mathf.FloorToInt((world.x - OriginX) / CellSize), Mathf.FloorToInt((world.y - OriginY) / CellSize));
        }

        public static Vector2 ToWorld(int x, int y)
        {
            return new Vector2(OriginX + (x + 0.5f) * CellSize, OriginY + (y + 0.5f) * CellSize);
        }

        /// <summary>На карті є кінематичні (рухомі скриптом) тіла чи анімовані об'єкти MapsExtended.</summary>
        private static bool HasMovingParts()
        {
            foreach (Rigidbody2D body in Object.FindObjectsOfType<Rigidbody2D>())
            {
                Transform root = body.transform.root;
                if (body.bodyType == RigidbodyType2D.Kinematic && root.GetComponent<Player>() == null
                    && root.GetComponent<ProjectileHit>() == null && root.GetComponent<SpawnedAttack>() == null)
                {
                    return true;
                }
            }

            if (!mapAnimationTypeLooked)
            {
                mapAnimationTypeLooked = true;
                mapAnimationType = HarmonyLib.AccessTools.TypeByName("MapsExt.MapObjectAnimation");
            }

            return mapAnimationType != null && Object.FindObjectOfType(mapAnimationType) != null;
        }

        private static NavGrid Build()
        {
            LayerMask mask = PlayerManager.instance.canSeePlayerMask;
            var solid = new bool[Width, Height];
            var marks = new char[Width, Height];
            bool movablesFound = false;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Vector2 center = ToWorld(x, y);
                    bool hazard = HazardCache.IsDangerous(center, -1f);
                    solid[x, y] = IsSolidAt(center, mask, out bool breakable, out bool movable) || hazard;
                    marks[x, y] = hazard ? 'S' : breakable ? 'D' : movable ? 'M' : ' ';
                    movablesFound |= movable || breakable;
                }
            }

            hasMovables = movablesFound;
            hasMovingParts = HasMovingParts();
            var built = new NavGrid(solid, PlayerClearance);
            ApplyLearned(built);
            MapWrapper map = MapManager.instance != null ? MapManager.instance.currentMap : null;
            if (!ReferenceEquals(map, dumpedFor))
            {
                dumpedFor = map; // файл — раз на карту, а не кожні 10 с
                DumpForDebugging(built, marks);
            }
            return built;
        }

        /// <summary>Назва поточної карти (сцени), напр. «12»; null — невідомо.</summary>
        public static string CurrentMapName()
        {
            MapManager manager = MapManager.instance;
            if (manager == null || manager.currentMap == null)
            {
                return null;
            }

            // Сцени модових карт часто звуться «NewMap», тож беремо назву з переліку карт.
            string[] levels = manager.levels;
            int id = manager.currentLevelID;
            string level = levels != null && id >= 0 && id < levels.Length ? levels[id] : null;
            return string.IsNullOrEmpty(level) ? manager.currentMap.Scene.name : System.IO.Path.GetFileNameWithoutExtension(level);
        }

        /// <summary>
        /// Зберігає сітку як текст — «мапа гри» для бота й для аналізу: navgrid_latest.txt і maps/&lt;карта&gt;.txt.
        /// # тверде, _ можна стояти, D руйнівне (перегородка/коробка), S пилка, цифра — бот, H — людина.
        /// </summary>
        private static void DumpForDebugging(NavGrid built, char[,] marks)
        {
            try
            {
                var players = PlayerManager.instance.players;
                string mapName = CurrentMapName() ?? "unknown";
                var text = new System.Text.StringBuilder();
                text.AppendLine($"map={mapName} standable={built.StandableCount} time={Time.time:F1} origin=({OriginX},{OriginY}) cell={CellSize}");
                text.AppendLine("legend: # solid, _ standable, D destructible, M movable box/ball, S saw/hazard, . air, digit bot, H human");
                for (int y = Height - 1; y >= 0; y--)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        char c = marks[x, y] != ' ' ? marks[x, y] : built.IsSolid(x, y) ? '#' : built.IsStandable(x, y) ? '_' : '.';
                        foreach (Player p in players)
                        {
                            if (p != null && ToCell(p.transform.position) == (x, y))
                            {
                                c = p.GetComponent<BotBrain>() != null ? (char)('0' + p.playerID % 10) : 'H';
                            }
                        }

                        text.Append(c);
                    }

                    text.AppendLine();
                }

                string folder = InputRecorder.Folder;
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "navgrid_latest.txt"), text.ToString());
                string mapsFolder = System.IO.Path.Combine(folder, "maps");
                System.IO.Directory.CreateDirectory(mapsFolder);
                string safeName = string.Join("_", mapName.Split(System.IO.Path.GetInvalidFileNameChars()));
                System.IO.File.WriteAllText(System.IO.Path.Combine(mapsFolder, safeName + ".txt"), text.ToString());
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"Nav grid dump failed: {ex.Message}");
            }
        }

        private static bool IsSolidAt(Vector2 center, LayerMask mask, out bool breakable, out bool movable)
        {
            breakable = false;
            movable = false;
            int count = Physics2D.OverlapBoxNonAlloc(center, ProbeSize, 0f, Buffer, mask);
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = Buffer[i];
                if (collider == null || collider.isTrigger)
                {
                    continue;
                }

                // Дерев'яні перегородки й коробки: на них можна стояти, тож у сітці вони тверді,
                // але позначаються «D» — коли перегородка на шляху, бот її прострілює (BotBrain).
                if (Destructibles.IsDestructible(collider))
                {
                    breakable = true;
                    return true;
                }

                // Рухомі ящики/кулі: тверді (стоїмо на них, перестрибуємо), але позначені «M» — сітка частіше оновлюється.
                if (Destructibles.IsMovable(collider))
                {
                    movable = true;
                    return true;
                }

                Transform root = collider.transform.root;
                if (root.GetComponent<Player>() == null && root.GetComponent<SpawnedAttack>() == null && root.GetComponent<ProjectileHit>() == null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
