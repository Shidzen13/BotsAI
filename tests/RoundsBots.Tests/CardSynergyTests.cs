using RoundsBots.Logic;
using Xunit;

public sealed class CardSynergyTests
{
    [Fact]
    public void ForbidsTacticalReloadWhenBotHasShieldsUp()
    {
        Assert.Equal(CardSynergy.ForbiddenPenalty, CardSynergy.Adjustment("TACTICAL RELOAD", new[] { "SHIELDS UP" }));
    }

    [Fact]
    public void ForbidsShieldsUpWhenBotHasTacticalReload_CaseInsensitive()
    {
        Assert.Equal(CardSynergy.ForbiddenPenalty, CardSynergy.Adjustment("Shields Up", new[] { "Tactical Reload" }));
    }

    [Fact]
    public void EachForbiddenCardAloneIsFine()
    {
        Assert.Equal(0f, CardSynergy.Adjustment("SHIELDS UP", new[] { "POISON" }));
        Assert.Equal(0f, CardSynergy.Adjustment("TACTICAL RELOAD", new string[0]));
    }

    [Fact]
    public void BonusWorksBothWays()
    {
        Assert.Equal(1.5f, CardSynergy.Adjustment("COMPASS", new[] { "TREASURE MAP" }), 3);
        Assert.Equal(1.5f, CardSynergy.Adjustment("Treasure Map", new[] { "Compass" }), 3);
    }

    [Fact]
    public void TricksterLikesBounces()
    {
        Assert.Equal(0.8f, CardSynergy.Adjustment("TRICKSTER", new[] { "BOUNCY", "MAYHEM" }), 3);
        Assert.Equal(0.8f, CardSynergy.Adjustment("BOUNCY", new[] { "TRICKSTER" }), 3);
    }

    [Fact]
    public void NoSynergy_ReturnsZero()
    {
        Assert.Equal(0f, CardSynergy.Adjustment("HUGE", new[] { "POISON", "ECHO" }));
        Assert.Equal(0f, CardSynergy.Adjustment(null, new[] { "POISON" }));
    }
}
