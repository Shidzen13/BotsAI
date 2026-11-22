using RoundsBots.Logic;
using Xunit;

namespace RoundsBots.Tests
{
    public sealed class SelfDamageCardTests
    {
        [Fact]
        public void DemonicPact_HasNoRiskPenalty_WhenSelfDamageDisabledByMod()
        {
            Assert.Equal(0f, CardKnowledge.Adjustment("Demonic pact", 0, selfDamageEnabled: false));
        }

        [Fact]
        public void DemonicPact_IsHeavilyPenalized_WhenSelfDamageHurts()
        {
            Assert.True(CardKnowledge.Adjustment("Demonic pact", 0, selfDamageEnabled: true) <= -4f);
        }

        [Fact]
        public void CardWithoutSelfDamage_KeepsRiskPenalty_RegardlessOfSelfDamageRule()
        {
            Assert.Equal(
                CardKnowledge.Adjustment("Abyssal Countdown", 0, selfDamageEnabled: true),
                CardKnowledge.Adjustment("Abyssal Countdown", 0, selfDamageEnabled: false));
        }
    }
}
