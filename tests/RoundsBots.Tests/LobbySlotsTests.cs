using RoundsBots.Logic;
using Xunit;

namespace RoundsBots.Tests
{
    public sealed class LobbySlotsTests
    {
        [Fact]
        public void FirstFreeSlot_SkipsHuman_WhenHumanHoldsFirstSlot()
        {
            Assert.Equal(1, LobbySlots.FirstFreeSlot(new[] { true, false }));
        }

        [Fact]
        public void FirstFreeSlot_ReturnsMinusOne_WhenAllSlotsTaken()
        {
            Assert.Equal(-1, LobbySlots.FirstFreeSlot(new[] { true, true }));
        }

        [Fact]
        public void FirstFreeColor_PicksUnusedColor_SoBotIsOwnTeam()
        {
            Assert.Equal(2, LobbySlots.FirstFreeColor(new[] { 0, 1, 3 }, 8));
        }

        [Fact]
        public void FirstFreeColor_FallsBackToZero_WhenAllColorsUsed()
        {
            Assert.Equal(0, LobbySlots.FirstFreeColor(new[] { 0, 1 }, 2));
        }
    }
}
