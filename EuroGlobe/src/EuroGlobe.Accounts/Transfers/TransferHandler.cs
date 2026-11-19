namespace EuroGlobe.Accounts.Transfers;

public abstract class TransferHandler
{
    private TransferHandler? _next;

    public TransferHandler SetNext(TransferHandler next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        return next;
    }

    public abstract Task HandleAsync(TransferContext context, CancellationToken cancellationToken);

    protected Task PassOnAsync(TransferContext context, CancellationToken cancellationToken)
        => _next is not null ? _next.HandleAsync(context, cancellationToken) : Task.CompletedTask;
}
