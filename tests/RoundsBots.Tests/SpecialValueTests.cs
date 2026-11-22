using RoundsBots.Logic;
using Xunit;

public sealed class SpecialValueTests
{
    [Theory]
    [InlineData("HOMING", 2.7f)]
    [InlineData("EXPLOSIVE BULLET", 2.8f)]
    [InlineData("BURST", 1.1f)]
    [InlineData("PHOENIX", 0f)]
    public void GeneratedTable_CarriesSpecialValues(string card, float expected)
    {
        Assert.True(CardKnowledge.TryGet(card, out CardTraits traits));
        Assert.Equal(expected, traits.Features!.SpecialValue, 3);
    }

    [Fact]
    public void AllKnownCards_HaveFiniteScores()
    {
        foreach (string card in new[] { "DEMONIC PACT", "HOMING", "PHOENIX", "SPRAY", "BURST", "TWIN SCYTHE", "ANGELIC" })
        {
            Assert.True(CardKnowledge.TryGet(card, out CardTraits traits));
            float score = CardScoring.Score(traits.Features!);
            Assert.False(float.IsNaN(score) || float.IsInfinity(score), card);
        }
    }

    [Fact]
    public void Phoenix_NowBeatsAngelic()
    {
        CardKnowledge.TryGet("PHOENIX", out CardTraits phoenix);
        CardKnowledge.TryGet("ANGELIC", out CardTraits angelic);
        Assert.True(CardScoring.Score(phoenix.Features!) > CardScoring.Score(angelic.Features!) + 1f);
    }

    [Fact]
    public void DemonicPact_LowWithSelfDamage_StrongWithout()
    {
        CardKnowledge.TryGet("DEMONIC PACT", out CardTraits pact);
        float baseScore = CardScoring.Score(pact.Features!);
        float withSelfDamage = baseScore + CardKnowledge.Adjustment("DEMONIC PACT", 0, selfDamageEnabled: true);
        float withoutSelfDamage = baseScore + CardKnowledge.Adjustment("DEMONIC PACT", 0, selfDamageEnabled: false);
        Assert.True(withSelfDamage < 0f);
        Assert.True(withoutSelfDamage > 2f && withoutSelfDamage < 5f);
    }
}
