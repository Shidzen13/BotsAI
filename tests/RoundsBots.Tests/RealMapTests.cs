using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RoundsBots.Logic;
using Xunit;
using Xunit.Abstractions;

namespace RoundsBots.Tests;

/// <summary>Навігація на реальних картах (дампи з гри, Maps\*.txt): чи досяжні гравці одне для одного.</summary>
public sealed class RealMapTests
{
    private readonly ITestOutputHelper output;

    public RealMapTests(ITestOutputHelper output) => this.output = output;

    public static IEnumerable<object[]> MapFiles() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Maps"), "*.txt").Select(f => new object[] { Path.GetFileName(f) });

    internal static NavGrid Load(string file, int clearance = 2) => LoadWith(file, clearance, 1);

    internal static NavGrid LoadWith(string file, int clearance, int jumps, bool breakables = false, Func<int, int, bool> removed = null)
    {
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Maps", file));
        int width = lines.Where(l => !l.Contains('=') && !l.StartsWith("legend")).Select(l => l.Length).DefaultIfEmpty(72).Max();
        string[] rows = lines.Where(l => l.Length == width && !l.Contains('=')).ToArray(); // ширина з 0.5.5 — за межами карти
        int height = rows.Length;
        var solid = new bool[width, height];
        var breakable = new bool[width, height];
        for (int row = 0; row < height; row++)
        {
            for (int x = 0; x < width; x++)
            {
                char c = rows[row][x];
                solid[x, height - 1 - row] = (c == '#' || c == 'D' || c == 'S') && !(removed != null && removed(x, height - 1 - row));
                breakable[x, height - 1 - row] = c == 'D';
            }
        }

        return new NavGrid(solid, clearance, jumps, breakables ? breakable : null);
    }

    [Theory]
    [MemberData(nameof(MapFiles))]
    public void Report_Connectivity(string file)
    {
        foreach (int clearance in new[] { 1 })
        {
            ReportFor(file, clearance);
        }
    }

    private void ReportFor(string file, int clearance)
    {
        NavGrid grid = Load(file, clearance);
        var cells = new List<(int x, int y)>();
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.IsStandable(x, y))
                {
                    cells.Add((x, y));
                }
            }
        }

        // Частка пар (вибірка) клітинок, між якими є маршрут.
        var random = new Random(1);
        int ok = 0;
        const int Samples = 150;
        for (int i = 0; i < Samples; i++)
        {
            var a = cells[random.Next(cells.Count)];
            var b = cells[random.Next(cells.Count)];
            if (a == b || grid.FindPath(a.x, a.y, b.x, b.y).Count > 0)
            {
                ok++;
            }
        }

        output.WriteLine($"{file} clearance={clearance}: standable={cells.Count} reachablePairs={ok * 100 / Samples}%");
    }

    [Theory]
    [InlineData("5025ca20-6bd3-45ca-94d7-f218fcfc4b4d.txt")]
    [InlineData("16901bda-f42f-4cfe-b87a-378d3078f4ff.txt")]
    [InlineData("41bcddbd-97c3-4289-9cd4-0aebbbfcc3ce.txt")]
    [InlineData("cfc89a2f-636a-47b3-a8c9-d6f42c2ab98a.txt", 27, 12)]
    [InlineData("e3a2753f-3d33-4ab1-a14c-cbc7e8157fed.txt", 10, 7)]
    public void Draw_Reachable(string file, int sx = -1, int sy = -1)
    {
        NavGrid grid = Load(file, 1);
        // Старт — задана клітинка або найнижча клітинка для стояння біля центру; малюємо, куди звідти дістатися.
        var start = sx >= 0 && grid.TryFindStandable(sx, sy, out int fx, out int fy) ? (x: fx, y: fy)
            : Enumerable.Range(0, grid.Height).SelectMany(y => Enumerable.Range(0, grid.Width).Select(x => (x, y)))
            .Where(c => grid.IsStandable(c.x, c.y)).OrderBy(c => Math.Abs(c.x - 36) + c.y * 0.1).First();
        output.WriteLine($"{file} start={start}");
        var reached = new HashSet<(int, int)>(grid.ReachableWithin(start.x, start.y, 10000f).Select(c => (c.x, c.y)));
        for (int y = grid.Height - 1; y >= 0; y--)
        {
            var line = new System.Text.StringBuilder();
            for (int x = 0; x < grid.Width; x++)
            {
                line.Append(reached.Contains((x, y)) ? 'o' : grid.IsSolid(x, y) ? '#' : grid.IsStandable(x, y) ? '_' : '.');
            }

            output.WriteLine($"{y,2} {line}");
        }
    }

    [Theory]
    [InlineData("c96fc4de-b555-4259-8a5e-887330b35f3f.txt", 55, 31, 11, 28)]
    [InlineData("35a41c0d-0389-4fea-a747-dadcd02a2db1.txt", 61, 31, 10, 33)]
    public void Draw_Path(string file, int sx, int sy, int gx, int gy)
    {
        NavGrid grid = Load(file, 1);
        Assert.True(grid.TryFindStandable(sx, sy, out int ax, out int ay));
        Assert.True(grid.TryFindStandable(gx, gy, out int bx, out int by));
        var path = grid.FindPath(ax, ay, bx, by);
        output.WriteLine($"start=({ax},{ay}) goal=({bx},{by}) steps={path.Count}");
        var marks = new Dictionary<(int, int), char>();
        int i = 0;
        foreach (NavStep step in path)
        {
            output.WriteLine($"{i}: {step.Move} -> ({step.X},{step.Y})");
            marks[(step.X, step.Y)] = step.Move == NavMove.Walk ? 'w' : step.Move == NavMove.Fall ? 'f' : step.Move == NavMove.Jump ? 'J' : 'C';
            i++;
        }

        marks[(ax, ay)] = 'S';
        for (int y = grid.Height - 1; y >= 0; y--)
        {
            var line = new System.Text.StringBuilder();
            for (int x = 0; x < grid.Width; x++)
            {
                line.Append(marks.TryGetValue((x, y), out char c) ? c : grid.IsSolid(x, y) ? '#' : grid.IsStandable(x, y) ? '_' : '.');
            }

            output.WriteLine($"{y,2} {line}");
        }
    }

    [Fact]
    public void RedBot_EscapesPit_ByBouncingOffArenaBottom()
    {
        // Карта e3a2753f: червоний бот стояв на платформі (5..10, 7) під масивом і не мав виходу.
        NavGrid grid = Load("e3a2753f-3d33-4ab1-a14c-cbc7e8157fed.txt", 1);

        var path = grid.FindPath(7, 7, 11, 14);

        Assert.NotEmpty(path);
        // З 0.5.5: ліва грань масиву (починається на 3 вище) — по ній вилазимо замість відскоку з ризиком.
        Assert.Contains(path, s => s.Move == NavMove.Bounce || s.Move == NavMove.Climb);
        Assert.NotEmpty(grid.FindPath(7, 7, 11, 14, allowBounce: false));
    }
}
