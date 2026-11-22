using System.Linq;
using RoundsBots.Logic;
using Xunit;

namespace RoundsBots.Tests;

public sealed class NavGridTests
{
    /// <summary>Будує сітку з текстової карти: '#' — тверде, рядки зверху вниз (верхній рядок — найвища y).</summary>
    private static NavGrid Parse(params string[] rows)
    {
        int width = rows[0].Length;
        int height = rows.Length;
        var solid = new bool[width, height];
        for (int row = 0; row < height; row++)
        {
            for (int x = 0; x < width; x++)
            {
                solid[x, height - 1 - row] = rows[row][x] == '#';
            }
        }

        return new NavGrid(solid);
    }

    [Fact]
    public void Standable_RequiresGroundBelowAndHeadroom()
    {
        var grid = Parse(
            "....",
            "....",
            "####");

        Assert.True(grid.IsStandable(1, 1));
        Assert.False(grid.IsStandable(1, 2)); // у повітрі
        Assert.False(grid.IsStandable(1, 0)); // у стіні
    }

    [Fact]
    public void FindPath_WalksAlongFloor()
    {
        var grid = Parse(
            "......",
            "......",
            "######");

        var path = grid.FindPath(0, 1, 5, 1);

        Assert.Equal(5, path.Count);
        Assert.All(path, s => Assert.Equal(NavMove.Walk, s.Move));
        Assert.Equal((5, 1), (path.Last().X, path.Last().Y));
    }

    [Fact]
    public void FindPath_DropsDownToLowerPlatform()
    {
        var grid = Parse(
            "..........",
            "..........",
            "####......",
            "..........",
            "..........",
            "##########");

        var path = grid.FindPath(1, 4, 8, 1);

        Assert.NotEmpty(path);
        Assert.Contains(path, s => s.Move == NavMove.Fall || s.Move == NavMove.Jump);
        Assert.Equal((8, 1), (path.Last().X, path.Last().Y));
    }

    [Fact]
    public void FindPath_JumpsUpToHigherPlatform()
    {
        var grid = Parse(
            "..........",
            "..........",
            "..........",
            "......####",
            "..........",
            "..........",
            "##########");

        var path = grid.FindPath(1, 1, 8, 4);

        Assert.NotEmpty(path);
        Assert.Contains(path, s => s.Move == NavMove.Jump);
        Assert.Equal((8, 4), (path.Last().X, path.Last().Y));
    }

    [Fact]
    public void FindPath_GoesAroundCeiling_WhenTargetIsAboveIt()
    {
        // Бот під широкою стелею, ціль над нею: треба вийти з-під стелі збоку і застрибнути.
        var grid = Parse(
            "............",
            "............",
            "..########..",
            "............",
            "............",
            "############");

        var path = grid.FindPath(5, 1, 5, 4);

        Assert.NotEmpty(path);
        Assert.Equal((5, 4), (path.Last().X, path.Last().Y));
        Assert.Contains(path, s => s.X <= 1 || s.X >= 10); // обійшов край стелі
    }

    [Fact]
    public void FindPath_JumpsOverGap()
    {
        var grid = Parse(
            "............",
            "............",
            "####....####");

        var path = grid.FindPath(1, 1, 10, 1);

        Assert.NotEmpty(path);
        Assert.Contains(path, s => s.Move == NavMove.Jump);
    }

    [Fact]
    public void FindPath_WalksUpSlope_WithoutJumping()
    {
        // Схил зі сходинками по 1 клітинці (так сітка бачить похилу поверхню).
        var grid = Parse(
            "........",
            "........",
            "........",
            "....####",
            "...#####",
            "..######",
            "########");

        var path = grid.FindPath(1, 1, 5, 4);

        Assert.NotEmpty(path);
        Assert.All(path, s => Assert.Equal(NavMove.Walk, s.Move));
    }

    [Fact]
    public void ReachableWithin_ReturnsSortedCells_WithinBudget()
    {
        var grid = Parse(
            "..........",
            "..........",
            "##########");

        var cells = grid.ReachableWithin(5, 1, 3f);

        Assert.Equal((5, 1, 0f), cells[0]);
        Assert.All(cells, c => Assert.True(c.cost <= 3f));
        Assert.Equal(7, cells.Count); // 5 ± 3 клітинки по підлозі
        Assert.True(cells.Select(c => c.cost).SequenceEqual(cells.Select(c => c.cost).OrderBy(c => c)));
    }

    [Fact]
    public void FindPath_DoesNotJumpToLedgeAboveSingleJumpHeight()
    {
        // Уступ на 10 клітинок вище: одинарним (≤6) не дістати, а подвійного без карток немає.
        var rows = new string[13];
        rows[0] = "............";
        rows[1] = "............";
        rows[2] = "......######";
        for (int i = 3; i < 12; i++)
        {
            rows[i] = "............";
        }

        rows[12] = "############";
        var grid = Parse(rows);

        Assert.Empty(grid.FindPath(1, 1, 8, 11));
    }

    [Fact]
    public void FindPath_Empty_WhenUnreachable()
    {
        var grid = Parse(
            "...#....",
            "...#....",
            "...#....",
            "...#....",
            "...#....",
            "...#....",
            "...#....",
            "########");

        Assert.Empty(grid.FindPath(1, 1, 6, 1));
    }

    [Fact]
    public void TryFindStandable_ScansDownFromAirToGround()
    {
        var grid = Parse(
            "....",
            "....",
            "....",
            "####");

        Assert.True(grid.TryFindStandable(2, 3, out int x, out int y));
        Assert.Equal((2, 1), (x, y));
    }
}

public sealed class NavGridPenaltyTests
{
    [Fact]
    public void Penalty_MakesPathfindingPreferAnotherRoute()
    {
        // Дві рівні дороги вгору: ліворуч і праворуч від стартового стовпа. Після штрафу на першу — береться друга.
        var solid = new bool[9, 8];
        for (int x = 0; x < 9; x++)
        {
            solid[x, 0] = true;
        }

        solid[1, 3] = solid[2, 3] = true;   // ліва полиця
        solid[6, 3] = solid[7, 3] = true;   // права полиця
        var grid = new NavGrid(solid, 1);

        var first = grid.FindPath(4, 1, 1, 4);
        Assert.NotEmpty(first);
        NavStep step = first[0];

        grid.AddPenalty(4, 1, step.X, step.Y, 100f);
        var second = grid.FindPath(4, 1, 1, 4);

        Assert.NotEmpty(second);
        Assert.NotEqual((step.X, step.Y, step.Move), (second[0].X, second[0].Y, second[0].Move));
    }
}
