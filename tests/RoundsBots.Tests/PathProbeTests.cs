using RoundsBots.Logic;
using RoundsBots.Tests;
using Xunit;

public sealed class PathProbeTests
{
    [Fact]
    public void BounceOverVoid_OpensRouteOutOfBasin_83eea289()
    {
        // Гра 2026-11-25: бот стояв під блоком, гравець — нагорі. Людина йде так: униз до прірви, відскок блоком
        // між плаваючими платформами (8–9 клітинок від берега), на платформу і вгору. Колона відскоку була за межею 6.
        NavGrid grid = RealMapTests.Load("83eea289-b5ea-497f-9a55-a2068a71380c.txt", 1);
        grid.TryFindStandable(71, 12, out int sx, out int sy);
        grid.TryFindStandable(71, 31, out int gx, out int gy);

        var path = grid.FindPath(sx, sy, gx, gy);

        Assert.NotEmpty(path);
        Assert.Contains(path, step => step.Move == NavMove.Bounce);
        Assert.Empty(grid.FindPath(sx, sy, gx, gy, allowBounce: false)); // без блоку дороги немає — бот має чекати на блок
    }

    [Fact]
    public void ClimbsRoomCorridor_6c87ded0()
    {
        // Гра 2026-11-25: бот у коридорі під кімнатою, гравець на полиці всередині. Людина: коридор між стіною арени й
        // кімнатою вгору на 21, через дах, униз праворуч і в кімнату. Раніше лазіння обмежувалось 16 — маршруту не було.
        NavGrid grid = RealMapTests.Load("6c87ded0-c455-4afa-83f9-dae8a7453fd2.txt", 1);
        grid.TryFindStandable(10, 15, out int sx, out int sy);
        grid.TryFindStandable(9, 31, out int gx, out int gy);

        var path = grid.FindPath(sx, sy, gx, gy);

        Assert.NotEmpty(path);
        Assert.Contains(path, step => step.Move == NavMove.Climb);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 9)]
    [InlineData(3, 13)]
    [InlineData(5, 13)]
    public void JumpHeightGrowsWithJumps(int jumps, int expected)
    {
        Assert.Equal(expected, NavGrid.JumpHeightFor(jumps));
    }
}

public sealed class PassageGapTests
{
    [Theory]
    [InlineData(0.3f, 1.3f, true)]   // щілина 1,6 у дні кімнати
    [InlineData(0.2f, 0.4f, false)]  // 0,6 — гравець не пролізе
    [InlineData(2.6f, 0.3f, false)]  // з одного боку відкритий простір — це не щілина, а край стіни
    [InlineData(1.2f, 1.2f, true)]
    [InlineData(1.5f, 1.5f, false)]  // ширше 2,6 — звичайний простір
    public void DetectsPassage(float forward, float backward, bool expected)
    {
        Assert.Equal(expected, RoundsBots.Logic.BotMath.IsPassageGap(forward, backward));
    }
}

public sealed class ApproachStickinessTests
{
    [Fact]
    public void KeepsPreviousApproachCell_WhenStillSeesTarget()
    {
        var cells = new[] { (5f, true, 10f), (2f, true, 12f) };
        Assert.Equal(1, RoundsBots.Logic.BotMath.ChooseApproachCell(cells, -1));
        Assert.Equal(0, RoundsBots.Logic.BotMath.ChooseApproachCell(cells, 0)); // не перескакуємо, поки звідти видно
    }

    [Fact]
    public void SwitchesToCellWithSight()
    {
        var cells = new[] { (1f, false, 5f), (9f, true, 20f) };
        Assert.Equal(1, RoundsBots.Logic.BotMath.ChooseApproachCell(cells, 0));
    }

    [Fact]
    public void KeepsNearCell_WhenNothingSees()
    {
        var cells = new[] { (1f, false, 6f), (9f, false, 5f) };
        Assert.Equal(0, RoundsBots.Logic.BotMath.ChooseApproachCell(cells, 0));   // різниця ≤2 — лишаємося
        Assert.Equal(1, RoundsBots.Logic.BotMath.ChooseApproachCell(new[] { (1f, false, 9f), (9f, false, 5f) }, 0));
    }
}

public sealed class CornerJumpTests
{
    [Fact]
    public void NoMaxHeightJumpOverPlatformCorner_RoadM()
    {
        // Гра 2026-11-25: бот стрибав з (69,10) на (63,15) — 5 угору через кут платформи — і бився об її бік.
        NavGrid grid = RealMapTests.Load("Road_M.txt", 1);
        var path = grid.FindPath(69, 10, 63, 15);

        Assert.NotEmpty(path);
        Assert.DoesNotContain(path, s => s.Move == NavMove.Jump && s.X == 63 && s.Y == 15 && path.IndexOf(s) == 0);
        Assert.NotEmpty(grid.FindPath(69, 10, 70, 28)); // і нагору, на дах до гравця, дорога лишається
    }
}

public sealed class GapClimbTests
{
    [Fact]
    public void ClimbsTowerFromSlope_OverOneColumnGap_3e40b284()
    {
        // Гра 2026-11-25: з верху схилу (29,18) права стіна вежі за одну вільну колонку — людина перестрибує до неї й
        // вилазить на вежу. Раніше лазіння рахувалося лише від стіни впритул — маршруту на вежу не було.
        NavGrid grid = RealMapTests.Load("3e40b284-39c2-47c8-971d-0965d699c3b2.txt", 1);
        grid.TryFindStandable(24, 27, out int gx, out int gy);

        var path = grid.FindPath(52, 11, gx, gy);

        Assert.NotEmpty(path);
        Assert.Contains(path, s => s.Move == NavMove.Climb);
    }
}

public sealed class JumpHoldTests
{
    [Fact]
    public void HoldsJump_FarBelowCeiling_EvenWithBigTakeoffSpeed()
    {
        // Одразу після поштовху vy ≈ 50 — без стелі оцінки бот відпускав кнопку й стрибав на половину висоти.
        Assert.False(RoundsBots.Logic.BotMath.ShouldReleaseJump(-9f, 50f, 40f));
        Assert.True(RoundsBots.Logic.BotMath.ShouldReleaseJump(13f, 50f, 40f));
        Assert.False(RoundsBots.Logic.BotMath.ShouldReleaseJump(10.8f, 20f, 40f)); // лазіння на висоті ~11 — тримаємо // під стелею — відпускаємо
    }
}

public sealed class BreakThroughTests
{
    [Fact]
    public void ReachesTopFloor_ByBreakingBoxLayer_9af3cbc5()
    {
        // Гра 2026-11-25: верхній поверх відгороджений шаром коробок без щілин — без «пробити» маршруту немає.
        NavGrid plain = RealMapTests.LoadWith("9af3cbc5-b3bc-4ad9-a944-47aff31ee2aa.txt", 1, 1);
        NavGrid breaking = RealMapTests.LoadWith("9af3cbc5-b3bc-4ad9-a944-47aff31ee2aa.txt", 1, 1, breakables: true);
        plain.TryFindStandable(73, 14, out int sx, out int sy);
        plain.TryFindStandable(60, 38, out int gx, out int gy);

        Assert.Empty(plain.FindPath(sx, sy, gx, gy));
        var path = breaking.FindPath(sx, sy, gx, gy);
        Assert.NotEmpty(path);
        Assert.Contains(path, s => s.Move == NavMove.Break);
    }
    [Fact]
    public void BoxedRoom_BreaksCeilingAndClimbsThrough_df5a7cb2()
    {
        // Гра 2026-11-25: кімната-коробка, стеля з коробок на 5 клітинок над підлогою — бот не ламав барикаду.
        const string map = "df5a7cb2-e7c1-489b-9992-63591e8401c5.txt";
        NavGrid breaking = RealMapTests.LoadWith(map, 1, 1, breakables: true);
        breaking.TryFindStandable(50, 3, out int sx, out int sy);
        breaking.TryFindStandable(23, 28, out int gx, out int gy);
        var path = breaking.FindPath(sx, sy, gx, gy);
        Assert.Contains(path, s => s.Move == NavMove.Break);

        // Після пролому стелі (x49-52, y7-8) кімната над нею досяжна звичайними рухами.
        NavGrid opened = RealMapTests.LoadWith(map, 1, 1, removed: (x, y) => x >= 49 && x <= 52 && (y == 7 || y == 8));
        Assert.NotEmpty(opened.FindPath(sx, sy, 53, 9));
    }
}
