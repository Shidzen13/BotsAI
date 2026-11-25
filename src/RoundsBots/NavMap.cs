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
        // Ширина сітки — за реальними межами карти (гра 2026-11-25: гравець ховався в кишені за x=-36, де бот «нічого не бачив»).
        private const float MinHalfWidth = 40f;
        private const float MaxHalfWidth = 80f;
        private const float BoundsMargin = 3f;
        private static float OriginX = -MinHalfWidth;
        private const float OriginY = -20f;
        private static int Width = (int)(2 * MinHalfWidth);
        private const int Height = 40;
        private static readonly Vector2 ProbeSize = new Vector2(0.9f, 0.9f);
        private static readonly Collider2D[] Buffer = new Collider2D[16];
        private static readonly RaycastHit2D[] RayBuffer = new RaycastHit2D[16];
        private static readonly Vector2 PointProbe = new Vector2(0.15f, 0.15f);

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

        /// <summary>Стрибок не долетів за час — легкий штраф лише цьому переходу.</summary>
        public static void ReportTimeout(int fromX, int fromY, int toX, int toY, string move)
        {
            EdgeLearning learning = LearningForCurrentMap();
            foreach ((int fx, int fy, int tx, int ty) in learning.RecordTimeout(fromX, fromY, toX, toY))
            {
                if (IsInside(fx, fy) && IsInside(tx, ty))
                {
                    grid?.SetPenalty(fx, fy, tx, ty, learning.PenaltyOf(fx, fy, tx, ty));
                }
            }

            BotLog.Write($"LEARN timeout {move} ({fromX},{fromY})->({toX},{toY}) penalty={learning.PenaltyOf(fromX, fromY, toX, toY):0.#}");
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

        /// <summary>Горизонтальні межі сітки — по всіх колайдерах поточної карти (не менше ±40, не більше ±80).</summary>
        private static void FitWidthToMap()
        {
            float minX = -MinHalfWidth;
            float maxX = MinHalfWidth;
            Map map = MapManager.instance != null && MapManager.instance.currentMap != null ? MapManager.instance.currentMap.Map : null;
            if (map != null)
            {
                foreach (Collider2D collider in map.GetComponentsInChildren<Collider2D>())
                {
                    if (collider != null && collider.enabled && !collider.isTrigger)
                    {
                        minX = Mathf.Min(minX, collider.bounds.min.x - BoundsMargin);
                        maxX = Mathf.Max(maxX, collider.bounds.max.x + BoundsMargin);
                    }
                }
            }

            minX = Mathf.Max(-MaxHalfWidth, Mathf.Floor(minX));
            maxX = Mathf.Min(MaxHalfWidth, Mathf.Ceil(maxX));
            OriginX = minX;
            Width = Mathf.Max(1, Mathf.RoundToInt((maxX - minX) / CellSize));
        }

        /// <summary>Найменша кількість стрибків серед ботів (сітка спільна — щоб маршрут був під силу кожному).</summary>
        private static int MinBotJumps()
        {
            int jumps = int.MaxValue;
            foreach (Player player in PlayerManager.instance.players)
            {
                if (player != null && player.GetComponent<BotBrain>() != null && player.data != null)
                {
                    jumps = Mathf.Min(jumps, player.data.jumps);
                }
            }

            return jumps == int.MaxValue ? 1 : Mathf.Max(1, jumps);
        }

        private static NavGrid Build()
        {
            LayerMask mask = PlayerManager.instance.canSeePlayerMask;
            FitWidthToMap();
            var solid = new bool[Width, Height];
            var marks = new char[Width, Height];
            bool movablesFound = false;
            int openedGaps = 0;
            var gapCandidate = new bool[Width, Height];
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Vector2 center = ToWorld(x, y);
                    bool hazard = HazardCache.IsDangerous(center, -1f);
                    solid[x, y] = IsSolidAt(center, mask, out bool breakable, out bool movable) || hazard;
                    gapCandidate[x, y] = solid[x, y] && !hazard && !breakable && !movable;

                    marks[x, y] = hazard ? 'S' : breakable ? 'D' : movable ? 'M' : ' ';
                    movablesFound |= movable || breakable;
                }
            }

            // Друга прохідка: відкриваємо щілину лише між суцільним з обох боків по її осі (гра 2026-11-25: тонкий
            // похилий дах над балкою «відкрився» як щілина — бот планував падати крізь дах).
            var original = (bool[,])solid.Clone();
            for (int x = 1; x < Width - 1; x++)
            {
                for (int y = 1; y < Height - 1; y++)
                {
                    if (!gapCandidate[x, y])
                    {
                        continue;
                    }

                    Vector2 center = ToWorld(x, y);
                    bool hole = original[x - 1, y] && original[x + 1, y] && IsNarrowPassage(center, mask, Vector2.right);
                    bool door = original[x, y - 1] && original[x, y + 1] && IsNarrowPassage(center, mask, Vector2.up);
                    if (hole || door)
                    {
                        solid[x, y] = false;
                        openedGaps++;
                    }
                }
            }

            hasMovables = movablesFound;
            if (openedGaps > 0)
            {
                Plugin.Log.LogInfo($"Nav grid: opened {openedGaps} narrow gap cells");
            }

            hasMovingParts = HasMovingParts();
            bool[,] breakables = null;
            if (movablesFound)
            {
                breakables = new bool[Width, Height];
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        breakables[x, y] = solid[x, y] && marks[x, y] == 'D';
                    }
                }
            }

            var built = new NavGrid(solid, PlayerClearance, MinBotJumps(), breakables);
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

        /// <summary>
        /// Вузький прохід (гра 2026-11-25: щілина в дні кімнати 1,6 од. — сітка 1×1 з пробою 0.9 бачила її стіною на
        /// всіх картах). Центр клітинки вільний, а вздовж осі вільний проміжок між двома стінами завширшки
        /// <see cref="BotMath.MinPassageGap"/>..<see cref="BotMath.MaxPassageGap"/> — гравець проходить.
        /// </summary>
        private static bool IsNarrowPassage(Vector2 center, LayerMask mask, Vector2 axis)
        {
            if (IsSolidAt(center, mask, out _, out _, PointProbe))
            {
                return false;
            }

            return BotMath.IsPassageGap(FreeDistance(center, axis, mask), FreeDistance(center, -axis, mask));
        }

        private static float FreeDistance(Vector2 from, Vector2 direction, LayerMask mask)
        {
            float nearest = BotMath.MaxPassageGap;
            int count = Physics2D.RaycastNonAlloc(from, direction, RayBuffer, BotMath.MaxPassageGap, mask);
            for (int i = 0; i < count; i++)
            {
                if (IsBlocking(RayBuffer[i].collider) && RayBuffer[i].distance < nearest)
                {
                    nearest = RayBuffer[i].distance;
                }
            }

            return nearest;
        }

        private static bool IsBlocking(Collider2D collider)
        {
            if (collider == null || collider.isTrigger)
            {
                return false;
            }

            Transform root = collider.transform.root;
            return root.GetComponent<Player>() == null && root.GetComponent<SpawnedAttack>() == null && root.GetComponent<ProjectileHit>() == null;
        }

        private static bool IsSolidAt(Vector2 center, LayerMask mask, out bool breakable, out bool movable)
        {
            return IsSolidAt(center, mask, out breakable, out movable, ProbeSize);
        }

        private static bool IsSolidAt(Vector2 center, LayerMask mask, out bool breakable, out bool movable, Vector2 size)
        {
            breakable = false;
            movable = false;
            int count = Physics2D.OverlapBoxNonAlloc(center, size, 0f, Buffer, mask);
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
