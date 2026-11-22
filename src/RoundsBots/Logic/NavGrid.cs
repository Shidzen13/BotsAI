using System;
using System.Collections.Generic;

namespace RoundsBots.Logic
{
    public enum NavMove
    {
        Walk,
        Fall,
        Jump,
        Climb,   // вгору по стіні відстрибуваннями (людина в записі піднімалася так до ~16)
        Bounce,  // падіння в порожнечу, блок об нижню межу арени (відскок 400) і політ угору
    }

    /// <summary>Крок маршруту: клітинка, куди перейти, і як.</summary>
    public readonly struct NavStep
    {
        public NavStep(int x, int y, NavMove move)
        {
            X = x;
            Y = y;
            Move = move;
        }

        public int X { get; }
        public int Y { get; }
        public NavMove Move { get; }
    }

    /// <summary>
    /// Навігаційна сітка карти. Вхід — карта твердих клітинок (true = стіна/платформа).
    /// Клітинка «придатна для стояння», якщо вона й клітинка над нею вільні, а під нею — твердо.
    /// Переходи: ходьба, зіскок униз, стрибок угору, стрибок через прірву. Пошук маршруту — A*.
    /// Без Unity, тому повністю покривається тестами.
    /// </summary>
    public sealed class NavGrid
    {
        // Запис гравця: одинарний стрибок піднімає на ≤6 (p90 5.5), подвійного без карток у грі немає (jumps = 1).
        public const int MaxJumpUp = 5;   // у грі стрибки на 6 часто не вдаються (LEARN fail), надійно — 5
        public const int MaxJumpSide = 7;
        public const int MaxGapJump = 10;
        public const int MaxGapDrop = 8;
        public const int MaxWallClimb = 16;
        public const int BounceHeight = 25;     // на скільки клітинок від низу арени підкидає відскок із блоком
        public const int BounceSideReach = 6;   // наскільки вбік можна зміститися в польоті після відскоку
        public const int BounceDrift = 6;       // наскільки далеко від краю може бути колона падіння

        private readonly bool[,] solid;
        private readonly Dictionary<int, List<(int node, NavMove move, float cost)>> edges = new Dictionary<int, List<(int, NavMove, float)>>();
        private readonly List<int> standable = new List<int>();
        private readonly Dictionary<long, float> penalties = new Dictionary<long, float>();

        /// <param name="clearance">Скільки вільних клітинок треба над опорою, щоб пройти (зріст гравця в клітинках).</param>
        public NavGrid(bool[,] solid, int clearance = 2)
        {
            this.solid = solid ?? throw new ArgumentNullException(nameof(solid));
            Clearance = Math.Max(1, clearance);
            Width = solid.GetLength(0);
            Height = solid.GetLength(1);
            Build();
        }

        public int Clearance { get; }

        public int Width { get; }

        public int Height { get; }

        public int StandableCount => standable.Count;

        /// <summary>Тверда клітинка. Боки й верх арени — тверді, низ — порожнеча.</summary>
        public bool IsSolid(int x, int y)
        {
            if (x < 0 || x >= Width || y >= Height)
            {
                return true;
            }

            return y >= 0 && solid[x, y];
        }

        public bool IsFree(int x, int y) => !IsSolid(x, y);

        public bool IsStandable(int x, int y)
        {
            return y >= 0 && y < Height && x >= 0 && x < Width
                && HasHeadroom(x, y) && y - 1 >= 0 && IsSolid(x, y - 1);
        }

        private bool HasHeadroom(int x, int y)
        {
            for (int i = 0; i < Clearance; i++)
            {
                if (IsSolid(x, y + i))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Найближча клітинка для стояння: спершу вниз по стовпцю, потім будь-яка найближча.</summary>
        public bool TryFindStandable(int x, int y, out int sx, out int sy)
        {
            x = Math.Max(0, Math.Min(Width - 1, x));
            y = Math.Max(0, Math.Min(Height - 1, y));
            for (int yy = y; yy >= 0; yy--)
            {
                if (IsStandable(x, yy))
                {
                    sx = x;
                    sy = yy;
                    return true;
                }

                if (IsSolid(x, yy))
                {
                    break;
                }
            }

            int best = -1;
            int bestDistance = int.MaxValue;
            foreach (int node in standable)
            {
                int nx = node % Width;
                int ny = node / Width;
                int distance = Math.Abs(nx - x) + Math.Abs(ny - y) * 2;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                }
            }

            sx = best >= 0 ? best % Width : 0;
            sy = best >= 0 ? best / Width : 0;
            return best >= 0;
        }

        /// <summary>
        /// Додатковий штраф переходу from→to (бот пробував і не зміг — «навчання на помилках»).
        /// A* обходить такі переходи, якщо є інший шлях.
        /// </summary>
        public void AddPenalty(int fromX, int fromY, int toX, int toY, float penalty)
        {
            long key = EdgeKey(Index(fromX, fromY), Index(toX, toY));
            penalties[key] = (penalties.TryGetValue(key, out float old) ? old : 0f) + penalty;
        }

        public float PenaltyOf(int fromX, int fromY, int toX, int toY)
        {
            return penalties.TryGetValue(EdgeKey(Index(fromX, fromY), Index(toX, toY)), out float value) ? value : 0f;
        }

        private static long EdgeKey(int from, int to) => ((long)from << 32) | (uint)to;

        /// <summary>A* від start до goal (обидві — клітинки для стояння). Порожній список, якщо шляху немає.</summary>
        public List<NavStep> FindPath(int startX, int startY, int goalX, int goalY)
        {
            var result = new List<NavStep>();
            if (!IsStandable(startX, startY) || !IsStandable(goalX, goalY))
            {
                return result;
            }

            int start = Index(startX, startY);
            int goal = Index(goalX, goalY);
            if (start == goal)
            {
                return result;
            }

            var cost = new Dictionary<int, float> { [start] = 0f };
            var cameFrom = new Dictionary<int, (int from, NavMove move)>();
            var open = new SortedSet<(float priority, int node)>(Comparer<(float, int)>.Create((a, b) =>
            {
                int c = a.Item1.CompareTo(b.Item1);
                return c != 0 ? c : a.Item2.CompareTo(b.Item2);
            }));
            open.Add((Heuristic(start, goal), start));

            while (open.Count > 0)
            {
                (float _, int current) = open.Min;
                open.Remove(open.Min);
                if (current == goal)
                {
                    break;
                }

                if (!edges.TryGetValue(current, out var neighbours))
                {
                    continue;
                }

                foreach ((int next, NavMove move, float stepCost) in neighbours)
                {
                    float penalty = penalties.Count > 0 && penalties.TryGetValue(EdgeKey(current, next), out float p) ? p : 0f;
                    float newCost = cost[current] + stepCost + penalty;
                    if (cost.TryGetValue(next, out float old) && old <= newCost)
                    {
                        continue;
                    }

                    if (cost.ContainsKey(next))
                    {
                        open.Remove((old + Heuristic(next, goal), next));
                    }

                    cost[next] = newCost;
                    cameFrom[next] = (current, move);
                    open.Add((newCost + Heuristic(next, goal), next));
                }
            }

            if (!cameFrom.ContainsKey(goal))
            {
                return result;
            }

            for (int node = goal; node != start; node = cameFrom[node].from)
            {
                result.Add(new NavStep(node % Width, node / Width, cameFrom[node].move));
            }

            result.Reverse();
            return result;
        }

        /// <summary>
        /// Усі клітинки для стояння, досяжні зі старту з вартістю ≤ <paramref name="maxCost"/> (Dijkstra),
        /// відсортовані за вартістю. Використовується для пошуку укриття.
        /// </summary>
        public List<(int x, int y, float cost)> ReachableWithin(int startX, int startY, float maxCost)
        {
            var result = new List<(int, int, float)>();
            if (!IsStandable(startX, startY))
            {
                return result;
            }

            int start = Index(startX, startY);
            var cost = new Dictionary<int, float> { [start] = 0f };
            var open = new SortedSet<(float, int)>(Comparer<(float, int)>.Create((a, b) =>
            {
                int c = a.Item1.CompareTo(b.Item1);
                return c != 0 ? c : a.Item2.CompareTo(b.Item2);
            }));
            open.Add((0f, start));
            var done = new HashSet<int>();

            while (open.Count > 0)
            {
                (float current, int node) = open.Min;
                open.Remove(open.Min);
                if (!done.Add(node))
                {
                    continue;
                }

                result.Add((node % Width, node / Width, current));
                if (!edges.TryGetValue(node, out var neighbours))
                {
                    continue;
                }

                foreach ((int next, NavMove _, float step) in neighbours)
                {
                    float newCost = current + step;
                    if (newCost > maxCost || done.Contains(next) || (cost.TryGetValue(next, out float old) && old <= newCost))
                    {
                        continue;
                    }

                    cost[next] = newCost;
                    open.Add((newCost, next));
                }
            }

            return result;
        }

        private float Heuristic(int a, int b)
        {
            // Допустима (не переоцінює) евристика: найдешевший перехід — ~0.5 за клітинку вбік, ~0.2 за клітинку вниз.
            return 0.5f * Math.Abs(a % Width - b % Width) + 0.2f * Math.Abs(a / Width - b / Width);
        }

        private int Index(int x, int y) => y * Width + x;

        private void Build()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (IsStandable(x, y))
                    {
                        standable.Add(Index(x, y));
                    }
                }
            }

            foreach (int node in standable)
            {
                int x = node % Width;
                int y = node / Width;
                var list = new List<(int, NavMove, float)>();
                AddWalksAndFalls(x, y, list);
                AddJumps(x, y, list);
                AddGapJumps(x, y, list);
                AddWallClimbs(x, y, list);
                AddBounces(x, y, list);
                edges[node] = list;
            }
        }

        private void AddWalksAndFalls(int x, int y, List<(int, NavMove, float)> list)
        {
            foreach (int dx in new[] { -1, 1 })
            {
                int nx = x + dx;

                // Схил: сусідня клітинка на 1 вище — у грі по ній просто йдуть, не стрибаючи.
                if (IsStandable(nx, y + 1) && IsFree(x, y + Clearance))
                {
                    list.Add((Index(nx, y + 1), NavMove.Walk, 1.2f));
                    continue;
                }

                if (!HasHeadroom(nx, y))
                {
                    continue;
                }

                if (IsStandable(nx, y))
                {
                    list.Add((Index(nx, y), NavMove.Walk, 1f));
                    continue;
                }

                // Зіскок: падаємо по стовпцю nx до першої опори.
                for (int ny = y - 1; ny >= 0; ny--)
                {
                    if (IsSolid(nx, ny))
                    {
                        break;
                    }

                    if (IsStandable(nx, ny))
                    {
                        list.Add((Index(nx, ny), NavMove.Fall, 1f + (y - ny) * 0.2f));
                        break;
                    }
                }
            }
        }

        private void AddJumps(int x, int y, List<(int, NavMove, float)> list)
        {
            for (int dy = 1; dy <= MaxJumpUp; dy++)
            {
                // Над головою має бути вільно на всю висоту стрибка.
                if (!IsFree(x, y + dy + Clearance - 1))
                {
                    break;
                }

                // Низький стрибок летить далі: запис гравця — до ~11 убік.
                int side = MaxJumpSide + (dy <= 2 ? 2 : dy <= 4 ? 1 : 0);
                for (int dx = -side; dx <= side; dx++)
                {
                    int tx = x + dx;
                    int ty = y + dy;
                    if (!IsStandable(tx, ty) || !IsRowClear(x, tx, ty + Clearance - 1))
                    {
                        continue;
                    }

                    list.Add((Index(tx, ty), NavMove.Jump, 2f + dy + Math.Abs(dx) * 0.5f));
                }
            }
        }

        private void AddGapJumps(int x, int y, List<(int, NavMove, float)> list)
        {
            foreach (int direction in new[] { -1, 1 })
            {
                // Стрибок через прірву має сенс лише на краю платформи.
                if (IsStandable(x + direction, y))
                {
                    continue;
                }

                for (int distance = 2; distance <= MaxGapJump + MaxGapDrop; distance++)
                {
                    int tx = x + direction * distance;
                    if (!IsRowClear(x, tx, y) || !IsRowClear(x, tx, y + Clearance - 1))
                    {
                        break; // на рівні стрибка стіна — далі не перестрибнути
                    }

                    // Що нижче приземлення, то довше летимо і то далі можна дострибнути.
                    for (int drop = Math.Max(0, distance - MaxGapJump); drop <= MaxGapDrop; drop++)
                    {
                        int ty = y - drop;
                        if (IsStandable(tx, ty) && IsColumnClear(tx, ty, y + Clearance - 1))
                        {
                            list.Add((Index(tx, ty), NavMove.Jump, 2f + distance * 0.5f + drop * 0.2f));
                            break;
                        }

                        if (IsSolid(tx, ty))
                        {
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Лазіння по стіні: поруч суцільна стіна — піднімаємося вздовж неї (наша колонка вільна)
        /// до її верху й виходимо на край зверху.
        /// </summary>
        private void AddWallClimbs(int x, int y, List<(int, NavMove, float)> list)
        {
            foreach (int direction in new[] { -1, 1 })
            {
                int wallX = x + direction;
                if (!IsSolid(wallX, y) || wallX < 0 || wallX >= Width)
                {
                    continue;
                }

                for (int h = y + 1; h <= y + MaxWallClimb && h < Height; h++)
                {
                    if (IsSolid(x, h + Clearance - 1))
                    {
                        break; // над головою стеля — далі не пролізти
                    }

                    if (IsSolid(wallX, h))
                    {
                        continue;
                    }

                    // Стіна скінчилася на висоті h: виходимо на її верх.
                    if (h - y > MaxJumpUp && IsStandable(wallX, h))
                    {
                        list.Add((Index(wallX, h), NavMove.Climb, 4f + (h - y) * 1.2f));
                    }

                    break;
                }

                AddClimbSideHops(x, y, wallX, list);
            }
        }

        /// <summary>
        /// Під час лазіння по стіні можна відстрибнути вбік на полицю (так людина піднімається біля стін арени):
        /// будь-яка клітинка для стояння вище за звичайний стрибок, до <see cref="MaxJumpSide"/> убік від нашої колонки.
        /// </summary>
        private void AddClimbSideHops(int x, int y, int wallX, List<(int, NavMove, float)> list)
        {
            for (int h = y + 1; h <= y + MaxWallClimb && h < Height; h++)
            {
                if (IsSolid(x, h + Clearance - 1))
                {
                    return; // стеля над колонкою лазіння
                }

                if (!IsSolid(wallX, h - 1))
                {
                    return; // стіна скінчилася — далі тримається лише «вихід на верх» вище
                }

                if (h - y <= MaxJumpUp)
                {
                    continue; // так високо дістає і звичайний стрибок
                }

                for (int dx = -MaxJumpSide; dx <= MaxJumpSide; dx++)
                {
                    int tx = x + dx;
                    if (dx == 0 || tx == wallX || !IsStandable(tx, h) || !IsRowClear(x, tx, h + Clearance - 1))
                    {
                        continue;
                    }

                    list.Add((Index(tx, h), NavMove.Climb, 4f + (h - y) * 1.2f + Math.Abs(dx) * 0.5f));
                }
            }
        }

        /// <summary>Колона падіння для відскоку з (x, y) на (lx, ly) — найближча, з якої такий перехід можливий; -1, якщо немає.</summary>
        public int FindBounceColumn(int x, int y, int lx, int ly)
        {
            for (int drift = 1; drift <= BounceDrift; drift++)
            {
                foreach (int direction in new[] { lx >= x ? 1 : -1, lx >= x ? -1 : 1 })
                {
                    int column = x + direction * drift;
                    if (IsRowClear(x + direction, column, y) && IsColumnClear(column, 0, y)
                        && IsColumnClear(column, y, ly + Clearance - 1) && Math.Abs(lx - column) <= BounceSideReach
                        && IsRowClear(column, lx, ly + Clearance - 1))
                    {
                        return column;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Відскок від нижньої межі арени: з краю над порожнечею падаємо колоною до низу, блокуємо удар
        /// і злітаємо вгору тією ж колоною, а потім зміщуємося вбік на полицю.
        /// </summary>
        private void AddBounces(int x, int y, List<(int, NavMove, float)> list)
        {
            var added = new HashSet<int>();
            foreach (int direction in new[] { -1, 1 })
            {
                for (int drift = 1; drift <= BounceDrift; drift++)
                {
                    int column = x + direction * drift;
                    if (!IsRowClear(x + direction, column, y) || !IsColumnClear(column, 0, y))
                    {
                        if (!IsRowClear(x + direction, column, y))
                        {
                            break;
                        }

                        continue; // під цією колоною є опора — це не порожнеча
                    }

                    for (int ty = 1; ty <= BounceHeight && ty < Height; ty++)
                    {
                        if (!IsFree(column, ty + Clearance - 1))
                        {
                            break; // колона польоту вгору впирається в перешкоду
                        }

                        for (int lx = column - BounceSideReach; lx <= column + BounceSideReach; lx++)
                        {
                            int node = Index(Math.Max(0, Math.Min(Width - 1, lx)), ty);
                            if (lx == x && ty == y || !IsStandable(lx, ty) || added.Contains(node)
                                || !IsRowClear(column, lx, ty + Clearance - 1))
                            {
                                continue;
                            }

                            added.Add(node);
                            list.Add((node, NavMove.Bounce, 12f + Math.Abs(lx - column) * 0.5f));
                        }
                    }
                }
            }
        }

        private bool IsRowClear(int fromX, int toX, int y)
        {
            int step = toX >= fromX ? 1 : -1;
            for (int x = fromX; x != toX + step; x += step)
            {
                if (IsSolid(x, y))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsColumnClear(int x, int fromY, int toY)
        {
            for (int y = Math.Min(fromY, toY); y <= Math.Max(fromY, toY); y++)
            {
                if (IsSolid(x, y))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
