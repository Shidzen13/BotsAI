using System;
using RoundsBots.Logic;
using Xunit;

namespace RoundsBots.Tests;

public sealed class BotMathTests
{
    [Theory]
    [InlineData(17f, true)]    // платформа під самою стелею — стрибок за межу
    [InlineData(12.6f, true)]
    [InlineData(12f, false)]
    [InlineData(0f, false)]
    public void JumpWouldHitCeiling_BlocksJumpsNearTopBound(float y, bool expected)
    {
        Assert.Equal(expected, BotMath.JumpWouldHitCeiling(y));
    }

    [Fact]
    public void ShortJump_AllowedBelowTopPlatforms()
    {
        Assert.False(BotMath.JumpWouldHitCeiling(14f, BotMath.ShortJumpApexEstimate));
        Assert.True(BotMath.JumpWouldHitCeiling(16f, BotMath.ShortJumpApexEstimate));
    }

    [Fact]
    public void ShouldReleaseJump_WhenApexPassesTopBound()
    {
        // 15 + 12²/(2·40) = 16.8 < 18.5 — ще можна; 17 + 1.8 = 18.8 ≥ 18.5 — відпускаємо.
        Assert.False(BotMath.ShouldReleaseJump(15f, 12f, 40f));
        Assert.True(BotMath.ShouldReleaseJump(17f, 12f, 40f));
    }

    [Fact]
    public void ShouldReleaseJump_NeverWhenFalling()
    {
        Assert.False(BotMath.ShouldReleaseJump(19f, -5f, 40f));
    }

    [Fact]
    public void Aim_ReturnsUnitVector()
    {
        var (x, y) = BotMath.Aim(10f, -3f, 5f);

        Assert.Equal(1f, MathF.Sqrt(x * x + y * y), 3);
    }

    [Fact]
    public void Aim_AimsAboveTarget_ToCompensateBulletDrop()
    {
        var (_, y) = BotMath.Aim(10f, 0f, 0f);

        Assert.True(y > 0f);
    }

    [Fact]
    public void Aim_PointsTowardsTarget_OnTheLeft()
    {
        var (x, _) = BotMath.Aim(-8f, 0f, 0f);

        Assert.True(x < 0f);
    }

    [Fact]
    public void Aim_HandlesZeroVector()
    {
        var (x, y) = BotMath.Aim(0f, -0f, 0f);

        Assert.False(float.IsNaN(x) || float.IsNaN(y));
    }

    [Theory]
    [InlineData(20f, 20f, 8f, true, 1)]    // далеко — йдемо до цілі
    [InlineData(-2f, 2f, 8f, true, 1)]     // занадто близько зліва — відходимо вправо
    [InlineData(8f, 8f, 8f, true, 0)]      // на потрібній дистанції — стоїмо
    [InlineData(-2f, 2f, 8f, false, -1)]   // не видно — йдемо до неї навіть зблизька
    public void Horizontal_KeepsPreferredDistance(float dx, float distance, float preferred, bool canSee, int expected)
    {
        Assert.Equal(expected, BotMath.Horizontal(dx, distance, preferred, canSee));
    }

    [Theory]
    [InlineData(20f, 0f, false)]
    [InlineData(-15f, 5f, false)]
    [InlineData(25f, -10f, true)]
    public void TryBallisticAim_TrajectoryHitsTarget(float dx, float dy, bool highArc)
    {
        const float speed = 60f;
        const float gravity = 30f;

        Assert.True(BotMath.TryBallisticAim(dx, dy, speed, gravity, highArc, out float dirX, out float dirY));

        float t = BotMath.FlightTime(dx, dirX, speed);
        var (x, y) = BotMath.BulletPosition(dirX, dirY, speed, gravity, t);
        Assert.Equal(dx, x, 2);
        Assert.Equal(dy, y, 2);
    }

    [Fact]
    public void TryBallisticAim_HighArcIsSteeperThanLowArc()
    {
        BotMath.TryBallisticAim(20f, 0f, 40f, 30f, false, out _, out float lowY);
        BotMath.TryBallisticAim(20f, 0f, 40f, 30f, true, out _, out float highY);

        Assert.True(highY > lowY);
    }

    [Fact]
    public void TryBallisticAim_FalseWhenOutOfRange()
    {
        Assert.False(BotMath.TryBallisticAim(500f, 0f, 20f, 30f, false, out _, out _));
    }

    [Fact]
    public void TryBallisticAim_StraightLineWithoutGravity()
    {
        Assert.True(BotMath.TryBallisticAim(3f, 4f, 50f, 0f, false, out float x, out float y));
        Assert.Equal(0.6f, x, 3);
        Assert.Equal(0.8f, y, 3);
    }

    [Theory]
    [InlineData(0f, -18f, 0f, -30f, 0.1f, true)]   // падає на нижню межу
    [InlineData(0f, -10f, 0f, -30f, 0.1f, false)]  // ще далеко
    [InlineData(34f, 0f, 30f, 0f, 0.1f, true)]     // летить у праву межу
    [InlineData(0f, 0f, 0f, 0f, 0.1f, false)]
    public void WillHitArenaBounds_PredictsOutOfBounds(float x, float y, float vx, float vy, float lookAhead, bool expected)
    {
        Assert.Equal(expected, BotMath.WillHitArenaBounds(x, y, vx, vy, lookAhead));
    }

    [Fact]
    public void IsIncomingThreat_True_WhenBulletFliesStraightAtBot()
    {
        // бот на 10 одиниць правіше кулі, куля летить вправо зі швидкістю 50
        Assert.True(BotMath.IsIncomingThreat(10f, 0f, 50f, 0f, 1.5f, 0.3f));
    }

    [Fact]
    public void IsIncomingThreat_False_WhenBulletFliesAway()
    {
        Assert.False(BotMath.IsIncomingThreat(10f, 0f, -50f, 0f, 1.5f, 0.3f));
    }

    [Fact]
    public void IsIncomingThreat_False_WhenBulletMisses()
    {
        Assert.False(BotMath.IsIncomingThreat(10f, 5f, 50f, 0f, 1.5f, 0.3f));
    }

    [Fact]
    public void IsIncomingThreat_False_WhenImpactIsOutsideTimeWindow()
    {
        Assert.False(BotMath.IsIncomingThreat(100f, 0f, 50f, 0f, 1.5f, 0.3f));
    }

    [Theory]
    [InlineData(15f, 0f, 0, 15f)]   // дальня збірка — тримаємо дистанцію
    [InlineData(15f, 0f, 2, 5f)]    // карти на блок — йдемо впритул
    [InlineData(15f, 10f, 0, 7f)]   // коротка дальність кулі
    [InlineData(15f, 2f, 0, 4f)]    // не ближче мінімуму
    public void PreferredDistance_DependsOnBuild(float baseDistance, float range, int closeEffects, float expected)
    {
        Assert.Equal(expected, BotMath.PreferredDistance(baseDistance, range, closeEffects), 3);
    }

    [Theory]
    [InlineData(-5f, -8f, true)]    // злетіли до -5 і знову падаємо — відскок витрачено
    [InlineData(-5f, -6f, false)]   // ще біля верхньої точки
    [InlineData(-17f, -19f, false)] // ще не злетіли після відскоку
    public void BounceSpent_AfterRiseAndFallAgain(float apex, float y, bool expected)
    {
        Assert.Equal(expected, BotMath.BounceSpent(apex, y, -16f, 2f));
    }

    [Theory]
    [InlineData(0f, 0f, true)]    // стін немає
    [InlineData(1f, 0f, false)]   // без дриля стіна блокує
    [InlineData(5f, 7f, true)]    // одна Drill Ammo (7 м) пробиває 5 м
    [InlineData(6f, 7f, false)]   // запас 80 %: 6 м уже ризиковано
    [InlineData(10f, 14f, true)]  // дві картки
    public void DrillPierces_ChecksWallThicknessAgainstDrill(float walls, float drill, bool expected)
    {
        Assert.Equal(expected, BotMath.DrillPierces(walls, drill));
    }

    [Theory]
    [InlineData(1, false, true, 0)]   // падаємо до відскоку, попереду стіна — не чіпляємося
    [InlineData(-1, false, false, -1)] // стіни немає — рухаємося до колони
    [InlineData(1, true, true, 1)]    // уже відскочили — летимо на полицю як завжди
    public void BounceFallHorizontal_AvoidsWallGrabBeforeBounce(int desired, bool bounced, bool wallAhead, int expected)
    {
        Assert.Equal(expected, BotMath.BounceFallHorizontal(desired, bounced, wallAhead));
    }
}
