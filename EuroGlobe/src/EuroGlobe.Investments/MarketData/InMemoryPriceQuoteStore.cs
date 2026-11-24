using System.Collections.Concurrent;
using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.MarketData;

public sealed class InMemoryPriceQuoteStore : IPriceQuoteStore
{
    private readonly ConcurrentDictionary<string, PriceQuote> _latest = new();

    public Task SaveAsync(PriceQuote quote, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quote);

        _latest[quote.AssetSymbol] = quote;

        return Task.CompletedTask;
    }

    public Task<PriceQuote?> GetLatestAsync(string assetSymbol, CancellationToken cancellationToken)
    {
        _latest.TryGetValue(assetSymbol.ToUpperInvariant(), out var quote);

        return Task.FromResult(quote);
    }
}