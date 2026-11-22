using RoundsBots.Logic;
using Xunit;

public sealed class HumanPicksTuningTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Poison_HasNoDuplicatePenalty(int copies)
    {
        Assert.Equal(0f, CardKnowledge.DuplicateAdjustment("POISON", copies));
    }

    [Fact]
    public void OtherCards_KeepDuplicatePenalty_EvenIfUnknown()
    {
        Assert.Equal(-0.7f, CardKnowledge.DuplicateAdjustment("HUGE", 1), 3);
        Assert.Equal(-0.7f, CardKnowledge.DuplicateAdjustment("NOT A REAL CARD", 1), 3);
    }

    [Fact]
    public void ShieldsUpAndRadarShot_BothOrders()
    {
        Assert.Equal(0.6f, CardSynergy.Adjustment("SHIELDS UP", new[] { "RADAR SHOT" }), 3);
        Assert.Equal(0.6f, CardSynergy.Adjustment("RADAR SHOT", new[] { "SHIELDS UP" }), 3);
    }

    [Fact]
    public void ShieldsUp_StillForbiddenWithTacticalReload_EvenWithRadar()
    {
        Assert.Equal(CardSynergy.ForbiddenPenalty, CardSynergy.Adjustment("SHIELDS UP", new[] { "RADAR SHOT", "TACTICAL RELOAD" }));
    }

    [Fact]
    public void DamageOverTimeTrio_AddsUpModestly()
    {
        Assert.Equal(0.5f, CardSynergy.Adjustment("PARASITE", new[] { "POISON" }), 3);
        Assert.Equal(0.9f, CardSynergy.Adjustment("POISON", new[] { "PARASITE", "LEECH" }), 3);
        Assert.Equal(0.4f, CardSynergy.Adjustment("LEECH", new[] { "POISON", "PARASITE" }), 3);
    }
}
