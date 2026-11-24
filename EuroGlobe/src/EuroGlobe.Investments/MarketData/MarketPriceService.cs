using EuroGlobe.Contracts.Time;
using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.MarketData;

public sealed class MarketPriceService : IMarketPriceService
{
    public static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(3);

    private readonly IAssetCatalog _catalog;
    private readonly IEnumerable<IPriceSource> _sources;
    private readonly IPriceQuoteStore _store;
    private readonly IClock _clock;

    public MarketPriceService(IAssetCatalog catalog, IEnumerable<IPriceSource> sources, IPriceQuoteStore store, IClock clock)
    {
        _catalog = catalog;
        _sources = sources;
        _store = store;
        _clock = clock;
    }

    public async Task<MarketPrice> GetCurrentPriceAsync(string assetSymbol, CancellationToken cancellationToken = default)
    {
        var asset = _catalog.Find(assetSymbol)
            ?? throw new ArgumentException($"Unknown asset {assetSymbol}.", nameof(assetSymbol));

        var source = _sources.FirstOrDefault(s => s.CanHandle(asset));

        if (source is not null)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(SourceTimeout);

                var price = await source.GetPriceEurAsync(asset, timeout.Token);
                var quote = new PriceQuote(asset.Symbol, price, _clock.UtcNow, source.Name);

                await _store.SaveAsync(quote, cancellationToken);

                return new MarketPrice(quote.AssetSymbol, quote.PriceEur, quote.FetchedAt, quote.Source, false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // External API failed or timed out: fall back to the last known price below.
            }
        }

        var last = await _store.GetLatestAsync(asset.Symbol, cancellationToken)
            ?? throw new PriceUnavailableException(asset.Symbol);

        return new MarketPrice(last.AssetSymbol, last.PriceEur, last.FetchedAt, last.Source, true);
    }
}