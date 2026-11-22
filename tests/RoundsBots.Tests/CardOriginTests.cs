using RoundsBots.Logic;
using Xunit;

namespace RoundsBots.Tests
{
    public sealed class CardOriginTests
    {
        [Theory]
        [InlineData("__KEYS__Alchemist", "KEYS")]
        [InlineData("__KEYS__Alchemist(Clone)", "KEYS")]
        [InlineData("__RSC__Sniper", "RSC")]
        public void Parse_ReadsModInitials_FromUnboundCardName(string objectName, string mod)
        {
            CardOrigin origin = CardOrigin.Parse(objectName);

            Assert.True(origin.IsModded);
            Assert.Equal(mod, origin.Mod);
        }

        [Theory]
        [InlineData("Shockwave(Clone)")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("____Broken")]
        public void Parse_TreatsCardsWithoutPrefix_AsVanilla(string? objectName)
        {
            Assert.False(CardOrigin.Parse(objectName!).IsModded);
        }

        [Fact]
        public void OriginAdjustment_PenalizesOnlyUnknownModdedCards()
        {
            CardOrigin modded = CardOrigin.Parse("__XYZ__Mystery");

            Assert.True(CardKnowledge.OriginAdjustment(modded, "Some Unknown Mystery Card") < 0f);
            Assert.Equal(0f, CardKnowledge.OriginAdjustment(modded, "Demonic pact"));
            Assert.Equal(0f, CardKnowledge.OriginAdjustment(CardOrigin.Parse("Mystery"), "Some Unknown Mystery Card"));
        }
    }
}
