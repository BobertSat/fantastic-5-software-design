namespace EuroGlobe.Accounts.Transfers;

public sealed class MockSepaGateway : ISepaGateway
{
    public Func<string, bool>? RejectRule { get; set; }

    public Task<bool> SendAsync(
        string fromIban,
        string toIban,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rejected = RejectRule?.Invoke(toIban) ?? false;
        return Task.FromResult(!rejected);
    }
}