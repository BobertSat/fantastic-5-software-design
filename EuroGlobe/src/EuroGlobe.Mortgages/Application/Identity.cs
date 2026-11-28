using System.Security.Claims;
using EuroGlobe.Mortgages.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace EuroGlobe.Mortgages.Application;

/// <summary>
/// Placeholder until member 1 publishes the shared current-user contract (ADR-M017).
/// </summary>
public interface ICurrentUser
{
    /// <summary>The signed-in user's id, or <c>null</c>.</summary>
    Guid? UserId { get; }

    /// <exception cref="MortgageUnauthenticatedException">No user is signed in.</exception>
    Guid RequireUserId() => UserId ?? throw new MortgageUnauthenticatedException();
}

/// <summary>
/// Placeholder until member 1 publishes the KYC-status contract (ADR-M017). Spec §11.1: an unverified
/// user gets 422 for mortgages.
/// </summary>
public interface IMortgageKycGate
{
    Task<bool> IsVerifiedAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Reads the user id from the <c>sub</c> / name-identifier claim. In Development only, it also accepts an
/// <c>X-User-Id</c> header so the API can be tried before authentication exists.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor, IHostEnvironment environment) : ICurrentUser
{
    public const string DevelopmentHeader = "X-User-Id";

    public Guid? UserId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null)
            {
                return null;
            }

            var claim = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(claim, out var fromClaim))
            {
                return fromClaim;
            }

            if (environment.IsDevelopment()
                && Guid.TryParse(context.Request.Headers[DevelopmentHeader].ToString(), out var fromHeader))
            {
                return fromHeader;
            }

            return null;
        }
    }
}

/// <summary>Treats every user as KYC-verified until member 1's KYC module exists (ADR-M017).</summary>
internal sealed class AlwaysVerifiedKycGate : IMortgageKycGate
{
    public Task<bool> IsVerifiedAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(true);
}
