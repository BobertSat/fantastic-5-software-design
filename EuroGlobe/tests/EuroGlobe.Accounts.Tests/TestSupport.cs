using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Tests.TestDoubles;
using EuroGlobe.Accounts.Transfers;
using EuroGlobe.Contracts.Interfaces;

namespace EuroGlobe.Accounts.Tests;

internal static class TestData
{
    /// <summary>A valid IBAN that does not belong to EuroGlobe (treated as external / SEPA).</summary>
    public const string ExternalIban = "DE89370400440532013000";

    public static IndividualAccount NewAccount(FakeClock clock, string currency = "EUR", decimal funds = 0m, Guid? ownerId = null)
    {
        var account = new IndividualAccount(ownerId ?? Guid.NewGuid(), IbanGenerator.Generate(), currency, clock);
        if (funds > 0)
        {
            account.Credit(funds);
        }

        return account;
    }

    public static TransferContext NewContext(IndividualAccount from, string toIban, decimal amount = 10m,
        string currency = "EUR", string totp = "123456")
        => new(from, toIban, amount, currency, totp, Guid.NewGuid());

    public static async Task AssertTransferFailsAsync(TransferFailure expected, Func<Task> act)
    {
        var ex = await Assert.ThrowsAsync<TransferException>(act);
        Assert.Equal(expected, ex.Failure);
    }
}

internal sealed record AuditEntry(Guid? UserId, string Action, string EntityType, string EntityId, string? Details);

internal sealed class FakeAuditLog : IAuditLog
{
    private readonly List<AuditEntry> _entries = [];

    public IReadOnlyList<AuditEntry> Entries
    {
        get
        {
            lock (_entries)
            {
                return _entries.ToList();
            }
        }
    }

    public Task LogAsync(Guid? userId, string action, string entityType, string entityId,
        string? details = null, CancellationToken cancellationToken = default)
    {
        lock (_entries)
        {
            _entries.Add(new AuditEntry(userId, action, entityType, entityId, details));
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeScaService : IScaService
{
    public bool Result { get; set; } = true;
    public int Calls { get; private set; }
    public Guid? LastUserId { get; private set; }
    public string? LastCode { get; private set; }

    public Task<bool> VerifyTotpAsync(Guid userId, string totpCode, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastUserId = userId;
        LastCode = totpCode;
        return Task.FromResult(Result);
    }
}

internal sealed class ThrowingSepaGateway : ISepaGateway
{
    public Task<bool> SendAsync(string fromIban, string toIban, decimal amount, string currency,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Gateway down.");
}

/// <summary>Terminal handler used to check whether a handler passed the request on.</summary>
internal sealed class RecordingHandler : TransferHandler
{
    public int Calls { get; private set; }

    public override Task HandleAsync(TransferContext context, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.CompletedTask;
    }
}
