using EuroGlobe.Accounts.Transfers;

namespace EuroGlobe.Accounts.Tests.Transfers;

public class MockSepaGatewayTests
{
    [Fact]
    public async Task WithoutRule_AlwaysSucceeds()
    {
        var gateway = new MockSepaGateway();

        Assert.True(await gateway.SendAsync("LT1", TestData.ExternalIban, 10m, "EUR"));
    }

    [Fact]
    public async Task RuleReturningTrue_RejectsTheTransfer()
    {
        var gateway = new MockSepaGateway { RejectRule = _ => true };

        Assert.False(await gateway.SendAsync("LT1", TestData.ExternalIban, 10m, "EUR"));
    }

    [Fact]
    public async Task RuleReceivesTheDestinationIban()
    {
        string? seen = null;
        var gateway = new MockSepaGateway { RejectRule = iban => { seen = iban; return false; } };

        var sent = await gateway.SendAsync("LT1", TestData.ExternalIban, 10m, "EUR");

        Assert.True(sent);
        Assert.Equal(TestData.ExternalIban, seen);
    }

    [Fact]
    public async Task CancelledToken_Throws()
    {
        var gateway = new MockSepaGateway();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => gateway.SendAsync("LT1", TestData.ExternalIban, 10m, "EUR", cts.Token));
    }
}
