using RoundsBots.Logic;
using Xunit;

public sealed class RwbIdeasTests
{
    [Fact]
    public void NotForBots_DetectedCaseInsensitive()
    {
        Assert.True(CardSynergy.IsNotForBots(new[] { "Default", "notforbots" }));
        Assert.False(CardSynergy.IsNotForBots(new[] { "Default" }));
        Assert.False(CardSynergy.IsNotForBots(null!));
    }

    [Fact]
    public void BulletSim_WithoutDrag_MatchesParabola()
    {
        var path = BulletSim.Path(1f, 0f, 50f, 30f, 0f, 1f, 1f, 0f, 0f, 1f);
        var last = path[path.Count - 1];
        Assert.Equal(50f, last.x, 0);
        Assert.InRange(last.y, -16f, -14f); // -g t^2 / 2 = -15 (дискретний крок дає трохи більше)
    }

    [Fact]
    public void BulletSim_DragSlowsBullet()
    {
        var plain = BulletSim.Path(1f, 0f, 50f, 0f, 0f, 1f, 1f, 0f, 0f, 1f);
        var dragged = BulletSim.Path(1f, 0f, 50f, 0f, 5f, 1f, 1f, 0f, 0f, 1f);
        Assert.True(dragged[dragged.Count - 1].x < plain[plain.Count - 1].x * 0.5f);
    }

    [Fact]
    public void BulletSim_TryAim_HitsTargetWithDrag()
    {
        bool ok = BulletSim.TryAim(10f, 0f, 50f, 30f, 3f, 1f, 1f, 0f, 0f, 0f, 45f, 3f, 0.5f, out float x, out float y, out float t);
        Assert.True(ok);
        Assert.True(y > 0f); // з гравітацією і опором треба цілити вище
        var miss = BulletSim.ClosestApproach(BulletSim.Path(x, y, 50f, 30f, 3f, 1f, 1f, 0f, 0f, 3f), 10f, 0f).miss;
        Assert.True(miss < 0.5f);
    }

    [Theory]
    [InlineData(true, 0, 9f, false, 0)]     // узятий стрибок маршруту — рятувальник не втручається
    [InlineData(false, 1, 0.4f, true, 1)]   // щойно стрибнули через прірву, попереду є опора — летимо далі
    [InlineData(false, -1, 0.4f, true, -1)]
    public void GapJumpEscape_KeepsJumpGoing(bool committed, int dir, float since, bool landing, int expected)
    {
        Assert.Equal(expected, BotMath.GapJumpEscape(committed, dir, since, landing));
    }

    [Theory]
    [InlineData(false, 1, 0.4f, false)] // попереду опори немає — рятуємося як раніше
    [InlineData(false, 1, 2.0f, true)]  // стрибок давно — звичайний порятунок
    [InlineData(false, 0, 0.1f, true)]  // не стрибали через прірву
    public void GapJumpEscape_FallsBackToNearestGround(bool committed, int dir, float since, bool landing)
    {
        Assert.Null(BotMath.GapJumpEscape(committed, dir, since, landing));
    }

    [Fact]
    public void BulletSim_NeededOnlyWithDragOrSpeedChange()
    {
        Assert.False(BulletSim.Needed(0f, 1f));
        Assert.True(BulletSim.Needed(1f, 1f));
        Assert.True(BulletSim.Needed(0f, 1.5f));
    }

    [Fact]
    public void ApproachCell_PrefersCheapestCellWithSight()
    {
        var cells = new[] { (cost: 1f, canSee: false, distanceToTarget: 2f), (cost: 9f, canSee: true, distanceToTarget: 12f), (cost: 5f, canSee: true, distanceToTarget: 15f) };
        Assert.Equal(2, BotMath.ChooseApproachCell(cells));
    }

    [Fact]
    public void ApproachCell_FallsBackToNearestWhenNothingSeesTarget()
    {
        var cells = new[] { (cost: 1f, canSee: false, distanceToTarget: 8f), (cost: 4f, canSee: false, distanceToTarget: 3f) };
        Assert.Equal(1, BotMath.ChooseApproachCell(cells));
        Assert.Equal(-1, BotMath.ChooseApproachCell(new (float, bool, float)[0]));
    }

    [Theory]
    [InlineData(false, false, true, false)] // перше застрягання на землі — ще пробуємо
    [InlineData(false, true, true, true)]   // той самий кут — розворот одразу
    [InlineData(false, false, false, true)] // висимо під виступом — розворот одразу
    [InlineData(true, false, true, true)]
    public void StuckReversal(bool repeated, bool sameSpot, bool grounded, bool expected)
    {
        Assert.Equal(expected, BotMath.ShouldReverseWhenStuck(repeated, sameSpot, grounded));
    }

    [Fact]
    public void StuckReverseGrowsAndCaps()
    {
        Assert.Equal(2.5f, BotMath.StuckReverseSeconds(2.5f, 0));
        Assert.Equal(4.5f, BotMath.StuckReverseSeconds(2.5f, 2));
        Assert.Equal(6f, BotMath.StuckReverseSeconds(2.5f, 10));
    }

    [Theory]
    [InlineData(0.02f, 0.5f, 0, 3f, 0f, 10f, true)]   // щойно зачепився за стіну — стрибаємо одразу
    [InlineData(0.02f, 0.05f, 1, 3f, 0f, 10f, false)] // гра ще не дасть стрибнути (0.1 с після стрибка)
    [InlineData(0.5f, 0.6f, 1, 0f, 0f, 10f, true)]    // вершина польоту, є подвійний — добираємо висоту
    [InlineData(0.5f, 0.6f, 0, 0f, 0f, 10f, false)]   // стрибків немає — чекаємо стіни
    [InlineData(0.02f, 0.5f, 1, 3f, 10f, 10f, false)] // уже на висоті краю — летимо на полицю
    public void ClimbJumpTiming(float sinceWall, float sinceJump, int jumps, float vy, float y, float ledge, bool expected)
    {
        Assert.Equal(expected, BotMath.ClimbJump(sinceWall, sinceJump, jumps, vy, y, ledge));
    }

    [Theory]
    [InlineData(-1f, 0f, true)]          // вертикальна стіна
    [InlineData(-0.5f, 0.866f, false)]   // схил 30° — вибігаємо
    [InlineData(-0.707f, 0.707f, false)] // 45° — ще схил
    [InlineData(-0.94f, 0.34f, true)]    // 70° — стіна
    [InlineData(0f, 0f, false)]
    public void SteepWallVsSlope(float nx, float ny, bool expected)
    {
        Assert.Equal(expected, BotMath.IsSteepWall(nx, ny));
    }
}
