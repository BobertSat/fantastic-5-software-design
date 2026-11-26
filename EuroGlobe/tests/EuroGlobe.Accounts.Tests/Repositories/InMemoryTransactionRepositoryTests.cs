using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Repositories;
using EuroGlobe.Accounts.Tests.TestDoubles;

namespace EuroGlobe.Accounts.Tests.Repositories;

public class InMemoryTransactionRepositoryTests
{
    private readonly FakeClock _clock = new();
    private readonly InMemoryTransactionRepository _repository = new();

    private Transaction Entry(Guid accountId, string description = "entry", decimal amount = 1m)
        => new(accountId, TransactionType.Deposit, amount, "EUR", description, _clock);

    private async Task AddManyAsync(Guid accountId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await _repository.AddAsync(Entry(accountId, i.ToString()));
        }
    }

    [Fact]
    public async Task List_ReturnsOnlyTheRequestedAccountsEntries_InInsertionOrder()
    {
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();
        await _repository.AddAsync(Entry(mine, "first"));
        await _repository.AddAsync(Entry(other, "not mine"));
        await _repository.AddAsync(Entry(mine, "second"));

        var list = await _repository.ListByAccountAsync(mine);

        Assert.Equal(["first", "second"], list.Select(t => t.Description));
    }

    [Fact]
    public async Task List_ForUnknownAccount_IsEmpty()
        => Assert.Empty(await _repository.ListByAccountAsync(Guid.NewGuid()));

    [Fact]
    public async Task List_ReturnsTheStoredTransactionInstances()
    {
        var account = Guid.NewGuid();
        var entry = Entry(account);
        await _repository.AddAsync(entry);

        var list = await _repository.ListByAccountAsync(account);

        Assert.Same(entry, Assert.Single(list));
    }

    [Fact]
    public async Task List_DefaultsToTheFirst100Entries()
    {
        var account = Guid.NewGuid();
        await AddManyAsync(account, 150);

        Assert.Equal(100, (await _repository.ListByAccountAsync(account)).Count);
        Assert.Equal(150, (await _repository.ListByAccountAsync(account, take: 1000)).Count);
    }

    [Fact]
    public async Task List_PagesWithSkipAndTake()
    {
        var account = Guid.NewGuid();
        await AddManyAsync(account, 10);

        var page = await _repository.ListByAccountAsync(account, take: 4, skip: 3);

        Assert.Equal(["3", "4", "5", "6"], page.Select(t => t.Description));
    }

    [Fact]
    public async Task List_SkipPastTheEnd_IsEmpty()
    {
        var account = Guid.NewGuid();
        await AddManyAsync(account, 5);

        Assert.Empty(await _repository.ListByAccountAsync(account, skip: 5));
        Assert.Empty(await _repository.ListByAccountAsync(account, skip: 50));
    }

    [Fact]
    public async Task List_TakeZero_IsEmpty()
    {
        var account = Guid.NewGuid();
        await AddManyAsync(account, 5);

        Assert.Empty(await _repository.ListByAccountAsync(account, take: 0));
    }

    [Fact]
    public async Task List_LastPageMayBeShorterThanTake()
    {
        var account = Guid.NewGuid();
        await AddManyAsync(account, 5);

        var page = await _repository.ListByAccountAsync(account, take: 10, skip: 3);

        Assert.Equal(["3", "4"], page.Select(t => t.Description));
    }

    [Fact]
    public async Task Add_WithCancelledToken_ThrowsAndStoresNothing()
    {
        var account = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _repository.AddAsync(Entry(account), cts.Token));

        Assert.Empty(await _repository.ListByAccountAsync(account));
    }

    [Fact]
    public async Task List_WithCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _repository.ListByAccountAsync(Guid.NewGuid(), cancellationToken: cts.Token));
    }
}
