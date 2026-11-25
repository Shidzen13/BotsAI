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
        Break,   // прострілити тонкий шар руйнівних блоків (стеля чи стінка) і пройти крізь нього
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
        public const int MaxWallClimb = 24;   // гра 2026-11-25: коридор між стіною арени й кімнатою — 21 угору (було 16)
        public const int WallReachUp = 3;     // стіна може починатися до 3 клітинок над підлогою — підстрибуємо й чіпляємося
        public const int BounceHeight = 25;     // на скільки клітинок від низу арени підкидає відскок із блоком
        public const int BounceSideReach = 6;   // наскільки вбік можна зміститися в польоті після відскоку
        /// <summary>Відскок витрачає блок і ризикує життям — лише коли звичайний маршрут довший на ~40 клітинок
        /// (гра 2026-11-25: з ціною 12 бот стрибав у прірву замість доступної дороги).</summary>
        public const float BounceCost = 45f;
        public const int BounceDrift = 10;      // наскільки далеко від краю може бути колона падіння (гра 2026-11-25: людина відскакує за 8–9 клітинок від берега, між платформами)

        private readonly bool[,] solid;
        private readonly Dictionary<int, List<(int node, NavMove move, float cost)>> edges = new Dictionary<int, List<(int, NavMove, float)>>();
        private readonly List<int> standable = new List<int>();
        private readonly Dictionary<long, float> penalties = new Dictionary<long, float>();

        /// <param name="clearance">Скільки вільних клітинок треба над опорою, щоб пройти (зріст гравця в клітинках).</param>
        /// <param name="jumps">Скільки стрибків у ботів: кожен додатковий (подвійний) дає ще <see cref="ExtraJumpUp"/> клітинок висоти.</param>
        /// <param name="boxCells">Клітинки, де на цій карті будь-коли стояли коробки (зокрема вже розбиті) — лише крізь
        /// них дозволено прямий високий стрибок; null = поточні <paramref name="breakable"/>.</param>
        public NavGrid(bool[,] solid, int clearance = 2, int jumps = 1, bool[,] breakable = null, bool[,] boxCells = null)
        {
            this.solid = solid ?? throw new ArgumentNullException(nameof(solid));
            this.breakable = breakable;
            this.boxCells = boxCells ?? breakable;
            JumpUp = JumpHeightFor(jumps);
            Clearance = Math.Max(1, clearance);
            Width = solid.GetLength(0);
            Height = solid.GetLength(1);
            Build();
        }

        public int Clearance { get; }

        private readonly bool[,] breakable;
        private readonly bool[,] boxCells;

        public const int MaxBreakThickness = 5;
        public const float BreakCost = 20f;

        /// <summary>Крізь дірку стрибаємо прямо вгору (без руху вбік) — дістаємо на клітинку вище за звичайний стрибок на полицю.</summary>
        public const int BreakExtraRise = 3;

        public bool IsBreakable(int x, int y)
        {
            return breakable != null && x >= 0 && y >= 0 && x < Width && y < Height && breakable[x, y] && IsSolid(x, y);
        }

        /// <summary>Перша руйнівна клітинка на переході Break з (fx, fy) до (tx, ty): угору — у нашій колонці, убік — у нашому ряду.</summary>
        public bool TryGetBreakCell(int fx, int fy, int tx, int ty, out int bx, out int by)
        {
            bx = fx;
            by = fy;
            if (ty > fy)
            {
                for (int h = fy + 1; h <= ty; h++)
                {
                    if (IsBreakable(fx, h))
                    {
                        by = h;
                        return true;
                    }
                }

                return false;
            }

            int step = tx > fx ? 1 : -1;
            for (int x = fx + step; x != tx; x += step)
            {
                if (IsBreakable(x, fy))
                {
                    bx = x;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Наскільки клітинок угору дістає стрибок з урахуванням подвійних (гра 2026-11-25: бот із 2 стрибками
        /// не бачив дороги на платформу на 10 вище, яку людина бере подвійним стрибком).</summary>
        public int JumpUp { get; }

        public const int ExtraJumpUp = 4;
        public const int MaxTotalJumpUp = 13;

        public static int JumpHeightFor(int jumps)
        {
            return Math.Min(MaxTotalJumpUp, MaxJumpUp + ExtraJumpUp * Math.Max(0, jumps - 1));
        }

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

        /// <summary>Задати штраф переходу (0 — прибрати).</summary>
        public void SetPenalty(int fromX, int fromY, int toX, int toY, float penalty)
        {
            long key = EdgeKey(Index(fromX, fromY), Index(toX, toY));
            if (penalty <= 0f)
            {
                penalties.Remove(key);
            }
            else
            {
                penalties[key] = penalty;
            }
        }

        public float PenaltyOf(int fromX, int fromY, int toX, int toY)
        {
            return penalties.TryGetValue(EdgeKey(Index(fromX, fromY), Index(toX, toY)), out float value) ? value : 0f;
        }

        private static long EdgeKey(int from, int to) => ((long)from << 32) | (uint)to;

        /// <summary>A* від start до goal (обидві — клітинки для стояння). Порожній список, якщо шляху немає.</summary>
        public List<NavStep> FindPath(int startX, int startY, int goalX, int goalY, bool allowBounce = true)
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
                    if (!allowBounce && move == NavMove.Bounce)
                    {
                        continue; // блок на перезарядці — відскок коштував би 51 HP
                    }

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
                AddWallHops(x, y, list);
                AddBounces(x, y, list);
                AddStraightUpJumps(x, y, list);
                if (breakable != null)
                {
                    AddBreakThroughs(x, y, list);
                }

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
            for (int dy = 1; dy <= JumpUp; dy++)
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

                    // Через кут платформи (між нами й ціллю під нею тверде) треба перелетіти вище краю й лише потім
                    // убік — на межі висоти це не вдається (гра 2026-11-25, Road_M: dy=5 через кут — удар у бік блоку).
                    if (dy > JumpUp - CornerMargin && HasSolidBetween(x, tx, ty - 1))
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
                // gap 0 — стіна впритул; gap 1 — між нами й стіною вільна колонка: перестрибуємо до стіни й чіпляємося
                // (гра 2026-11-25, 3e40b284: людина з верху схилу стрибає до правої стіни вежі й вилазить на неї).
                for (int gap = 0; gap <= 1; gap++)
                {
                    int climbX = x + direction * gap;
                    int wallX = climbX + direction;
                    if (gap == 1 && (climbX < 0 || climbX >= Width || IsSolid(climbX, y) || IsStandable(climbX, y)))
                    {
                        continue; // у сусідню колонку не стати / з неї лазіння рахується окремо
                    }

                    int wallStart = WallStartAbove(climbX, y, wallX, JumpUp);
                    if (wallStart < 0)
                    {
                        continue;
                    }

                    AddClimbToTop(x, y, climbX, wallX, wallStart, gap, list);
                    AddClimbSideHops(climbX, y, wallX, wallStart, list, gap);
                }
            }
        }

        private void AddClimbToTop(int x, int y, int climbX, int wallX, int wallStart, int gap, List<(int, NavMove, float)> list)
        {
            for (int h = wallStart + 1; h <= y + MaxWallClimb && h < Height; h++)
            {
                if (IsSolid(climbX, h + Clearance - 1))
                {
                    return; // над головою стеля — далі не пролізти
                }

                if (IsSolid(wallX, h))
                {
                    continue;
                }

                // Стіна скінчилася на висоті h: виходимо на її верх.
                if (h - y > JumpUp && IsStandable(wallX, h))
                {
                    list.Add((Index(wallX, h), NavMove.Climb, 4f + gap + (h - y) * 1.2f));
                }

                return;
            }
        }

        /// <summary>Колонка, вздовж якої лізти з (x, y) у бік towardX: наша чи сусідня (стіна за проміжком); null — немає.</summary>
        public int? ClimbColumn(int x, int y, int towardX)
        {
            return TryGetClimbWall(x, y, towardX, out int climbX, out _) ? climbX : (int?)null;
        }

        /// <summary>Колонка лазіння й бік стіни (−1/+1) з (x, y) у бік towardX.</summary>
        public bool TryGetClimbWall(int x, int y, int towardX, out int climbX, out int wallDirection)
        {
            int direction = towardX >= x ? 1 : -1;
            foreach (int dir in new[] { direction, -direction })
            {
                for (int gap = 0; gap <= 1; gap++)
                {
                    climbX = x + dir * gap;
                    if (gap == 1 && (climbX < 0 || climbX >= Width || IsSolid(climbX, y)))
                    {
                        continue;
                    }

                    if (WallStartAbove(climbX, y, climbX + dir, JumpUp) >= 0)
                    {
                        wallDirection = dir;
                        return true;
                    }
                }
            }

            climbX = x;
            wallDirection = 0;
            return false;
        }

        /// <summary>
        /// Де починається стіна поруч: на нашій висоті або до <see cref="WallReachUp"/> клітинок вище — до неї підстрибуємо
        /// і чіпляємося (гра 2026-11-25: стіни кімнат і ніжки платформ висять над підлогою, бот їх не бачив). -1 — стіни немає.
        /// </summary>
        private int WallStartAbove(int x, int y, int wallX, int reach)
        {
            if (wallX < 0 || wallX >= Width)
            {
                return -1;
            }

            for (int h = y; h <= y + reach && h < Height; h++)
            {
                if (h > y && IsSolid(x, h + Clearance - 1))
                {
                    return -1;
                }

                if (IsSolid(wallX, h))
                {
                    return h;
                }
            }

            return -1;
        }

        /// <summary>
        /// Під час лазіння по стіні можна відстрибнути вбік на полицю (так людина піднімається біля стін арени):
        /// будь-яка клітинка для стояння вище за звичайний стрибок, до <see cref="MaxJumpSide"/> убік від нашої колонки.
        /// </summary>
        private void AddClimbSideHops(int x, int y, int wallX, int wallStart, List<(int, NavMove, float)> list, int gap = 0)
        {
            for (int h = y + 1; h <= y + MaxWallClimb && h < Height; h++)
            {
                if (IsSolid(x, h + Clearance - 1))
                {
                    return; // стеля над колонкою лазіння
                }

                if (h - 1 >= wallStart && !IsSolid(wallX, h - 1))
                {
                    return; // стіна скінчилася — далі тримається лише «вихід на верх» вище
                }

                if (h - y <= JumpUp)
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

                    list.Add((Index(tx, h), NavMove.Climb, 4f + gap + (h - y) * 1.2f + Math.Abs(dx) * 0.5f));
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
                            list.Add((node, NavMove.Bounce, BounceCost + Math.Abs(lx - column) * 0.5f));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Крізь руйнівне (гра 2026-11-25, 9af3cbc5: верхній поверх відгороджений шаром коробок без щілин — людина
        /// прострілює дірку). Угору: над нами шар до <see cref="MaxBreakThickness"/>, над ним стаємо поруч з діркою.
        /// Убік: стінка з коробок у нашому ряду, за нею клітинка для стояння.
        /// </summary>
        private void AddBreakThroughs(int x, int y, List<(int, NavMove, float)> list)
        {
            int bottom = -1;
            for (int h = y + 1; h <= y + Math.Min(JumpUp + BreakExtraRise, MaxTotalJumpUp) && h < Height; h++)
            {
                if (IsSolid(x, h))
                {
                    bottom = IsBreakable(x, h) ? h : -1;
                    break;
                }
            }

            if (bottom >= 0)
            {
                int top = bottom;
                while (top + 1 < Height && IsBreakable(x, top + 1))
                {
                    top++;
                }

                int land = top + 1;
                if (top - bottom < MaxBreakThickness && land - y <= Math.Min(JumpUp + BreakExtraRise, MaxTotalJumpUp) && IsFree(x, land + Clearance - 1))
                {
                    foreach (int dx in new[] { -1, 1 })
                    {
                        if (IsStandable(x + dx, land))
                        {
                            list.Add((Index(x + dx, land), NavMove.Break, BreakCost + (top - bottom + 1) * 3f));
                        }
                    }
                }
            }

            foreach (int direction in new[] { -1, 1 })
            {
                int k = 0;
                while (k < MaxBreakThickness && IsBreakable(x + direction * (k + 1), y))
                {
                    k++;
                }

                int tx = x + direction * (k + 1);
                if (k > 0 && IsStandable(tx, y) && (Clearance < 2 || !IsSolid(x + direction, y + 1) || IsBreakable(x + direction, y + 1)))
                {
                    list.Add((Index(tx, y), NavMove.Break, BreakCost + k * 3f));
                }
            }
        }

        public const float StraightJumpExtraCost = 4f;

        /// <summary>
        /// Високий стрибок прямо вгору з повним утриманням крізь отвір у стелі (гра 2026-11-25, df5a7cb2: бот пробив стелю
        /// коробки на 5 клітинок над підлогою, а далі вгору не мав маршруту). Колона над нами вільна, приземлення поруч з
        /// отвором, на JumpUp+1..JumpUp+<see cref="BreakExtraRise"/> вище.
        /// </summary>
        private void AddStraightUpJumps(int x, int y, List<(int, NavMove, float)> list)
        {
            if (boxCells == null)
            {
                return;
            }

            for (int h = y + 1; h <= y + JumpUp + Clearance - 1; h++)
            {
                if (h >= Height || IsSolid(x, h))
                {
                    return;
                }
            }

            int maxLand = y + Math.Min(JumpUp + BreakExtraRise, MaxTotalJumpUp);
            for (int land = y + JumpUp + 1; land <= maxLand && land + Clearance - 1 < Height; land++)
            {
                if (IsSolid(x, land + Clearance - 1) || IsSolid(x, land - 1))
                {
                    return;
                }

                if (!WasBoxColumn(x, y + 1, land))
                {
                    continue;
                }

                foreach (int dx in new[] { -1, 1 })
                {
                    if (IsStandable(x + dx, land))
                    {
                        list.Add((Index(x + dx, land), NavMove.Jump, 3f + (land - y) + StraightJumpExtraCost));
                    }
                }
            }
        }

        /// <summary>Чи стояла коробка в колоні x між рядами from..to (отвір, пробитий у стелі).</summary>
        private bool WasBoxColumn(int x, int from, int to)
        {
            for (int h = Math.Max(0, from); h <= to && h < Height; h++)
            {
                if (boxCells[x, h])
                {
                    return true;
                }
            }

            return false;
        }

        public const int MaxHopWallWidth = 3;
        public const int MaxHopLandingReach = 4;

        /// <summary>
        /// Стрибок через невисоку стінку (гра 2026-11-25, торт: свічка — 3 клітинки, бот не вмів через неї
        /// перестрибнути — лише на її вузький верх). Стінка впритул, ширина до <see cref="MaxHopWallWidth"/>, висота до
        /// JumpUp−1; за нею приземлення не вище за верх стінки.
        /// </summary>
        private void AddWallHops(int x, int y, List<(int, NavMove, float)> list)
        {
            foreach (int direction in new[] { -1, 1 })
            {
                if (!IsSolid(x + direction, y))
                {
                    continue;
                }

                int width = 0;
                int top = y;
                while (width < MaxHopWallWidth && IsSolid(x + direction * (width + 1), y))
                {
                    int column = x + direction * (width + 1);
                    int h = y;
                    while (h < Height && IsSolid(column, h))
                    {
                        h++;
                    }

                    top = Math.Max(top, h);
                    width++;
                }

                int wallEnd = x + direction * width;
                if (IsSolid(wallEnd + direction, y) || top - y > JumpUp - CornerMargin
                    || !IsColumnClear(x, y, top + Clearance - 1) || !IsRowClear(x, wallEnd + direction, top + Clearance - 1))
                {
                    continue;
                }

                for (int reach = 1; reach <= MaxHopLandingReach; reach++)
                {
                    int lx = wallEnd + direction * reach;
                    if (!IsRowClear(wallEnd + direction, lx, top + Clearance - 1))
                    {
                        break;
                    }

                    for (int ly = top; ly >= Math.Max(0, y - MaxGapDrop); ly--)
                    {
                        if (IsStandable(lx, ly))
                        {
                            list.Add((Index(lx, ly), NavMove.Jump, 3f + (top - y) + Math.Abs(lx - x) * 0.5f));
                            break;
                        }

                        if (IsSolid(lx, ly))
                        {
                            break;
                        }
                    }
                }
            }
        }

        public const int CornerMargin = 1;

        /// <summary>Чи є тверде в ряду y строго між колонками a і b.</summary>
        private bool HasSolidBetween(int a, int b, int y)
        {
            int step = b >= a ? 1 : -1;
            for (int x = a + step; x != b; x += step)
            {
                if (IsSolid(x, y))
                {
                    return true;
                }
            }

            return false;
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
