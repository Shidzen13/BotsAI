using RoundsBots.Logic;
using Xunit;

namespace RoundsBots.Tests
{
    public sealed class DeathCauseTests
    {
        [Fact]
        public void Classify_ReturnsPlayer_WhenAnotherPlayerDealtDamage()
        {
            Assert.Equal(DamageCause.Player, DeathCause.Classify(2, 0, 0f, 0f));
        }

        [Fact]
        public void Classify_ReturnsSelf_WhenOwnBulletHit()
        {
            Assert.Equal(DamageCause.Self, DeathCause.Classify(1, 1, 0f, 0f));
        }

        [Theory]
        [InlineData(0f, -19.5f)]
        [InlineData(34.5f, 0f)]
        [InlineData(-35f, 5f)]
        public void Classify_ReturnsBounds_WhenNoSourceNearArenaEdge(float x, float y)
        {
            Assert.Equal(DamageCause.Bounds, DeathCause.Classify(-1, 0, x, y));
        }

        [Fact]
        public void Classify_ReturnsHazard_WhenNoSourceInsideArena()
        {
            Assert.Equal(DamageCause.Hazard, DeathCause.Classify(-1, 0, 3f, -2f));
        }

        [Fact]
        public void IsVariantB_StaysFixedWithinMatch_AndSwapsNextMatch()
        {
            Assert.False(DeathCause.IsVariantB(0, 1) == DeathCause.IsVariantB(0, 2));
            Assert.False(DeathCause.IsVariantB(0, 1) == DeathCause.IsVariantB(1, 1));
        }
    }
}
