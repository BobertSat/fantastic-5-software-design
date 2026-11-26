using EuroGlobe.Contracts.Interfaces;

namespace EuroGlobe.Accounts.Transfers;

public sealed class ScaHandler(IScaService sca) : TransferHandler
{
    private readonly IScaService _sca = sca ?? throw new ArgumentNullException(nameof(sca));

    public override async Task HandleAsync(TransferContext context, CancellationToken cancellationToken)
    {
        var valid = await _sca.VerifyTotpAsync(context.From.OwnerId, context.TotpCode, cancellationToken);
        if (!valid)
        {
            throw new TransferException(TransferFailure.Forbidden, "Invalid TOTP code.");
        }

        await PassOnAsync(context, cancellationToken);
    }
}
