using RoundsBots.Logic;
using Xunit;

namespace RoundsBots.Tests
{
    public sealed class StalemateTests
    {
        [Fact]
        public void ShouldApproach_WhenTargetVisibleButNoShotForLong()
        {
            Assert.True(Stalemate.ShouldApproach(canSee: true, secondsWithoutClearShot: 3.5f));
        }

        [Fact]
        public void ShouldNotApproach_WhenShotWasRecent()
        {
            Assert.False(Stalemate.ShouldApproach(canSee: true, secondsWithoutClearShot: 1f));
        }

        [Fact]
        public void PreferredDistance_Unchanged_WhileFightIsActive()
        {
            Assert.Equal(15f, Stalemate.PreferredDistance(15f, 4f));
        }

        [Fact]
        public void PreferredDistance_ShrinksToClose_AfterLongQuiet()
        {
            Assert.Equal(10f, Stalemate.PreferredDistance(15f, Stalemate.QuietSeconds + Stalemate.ShrinkSeconds / 2), 3);
            Assert.Equal(Stalemate.CloseDistance, Stalemate.PreferredDistance(15f, 60f), 3);
        }

        [Fact]
        public void ShouldForceEnd_WhenOnlyOwnBotsAliveAndQuietForMinute()
        {
            Assert.True(Stalemate.ShouldForceEnd(aliveOthers: 0, aliveOwnBots: 2, secondsSinceAnyDamage: 61f));
        }

        [Theory]
        [InlineData(1, 2, 120f)] // жива людина або чужий бот — ніколи
        [InlineData(0, 1, 120f)] // лишився один — очко й так закінчується
        [InlineData(0, 3, 59f)]  // ще не хвилина тиші
        public void ShouldNotForceEnd_OtherwiseOrTooEarly(int others, int ownBots, float quiet)
        {
            Assert.False(Stalemate.ShouldForceEnd(others, ownBots, quiet));
        }

        [Theory]
        [InlineData(59.9f, false)]
        [InlineData(60f, true)]
        public void ShouldForceEnd_RespectsThresholdBoundary(float quiet, bool expected)
        {
            Assert.Equal(expected, Stalemate.ShouldForceEnd(0, 2, 2, quiet, 60f));
        }

        [Fact]
        public void ShouldNotForceEnd_WhenAllAliveBotsAreOneTeam()
        {
            Assert.False(Stalemate.ShouldForceEnd(0, 2, aliveTeams: 1, secondsSinceAnyDamage: 120f, thresholdSeconds: 60f));
        }

        [Fact]
        public void PreferredDistance_NeverGrows_ForCloseRangeBuilds()
        {
            Assert.Equal(4f, Stalemate.PreferredDistance(4f, 60f), 3);
        }
    }
}
