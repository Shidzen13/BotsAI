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
}
