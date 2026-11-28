using System.Security.Claims;
using EuroGlobe.Mortgages.Application;
using EuroGlobe.Mortgages.Domain;
using Microsoft.AspNetCore.Http;

namespace EuroGlobe.Mortgages.Tests;

public sealed class IdentityPlaceholderTests
{
    private static readonly Guid UserA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ICurrentUser CurrentUser(HttpContext? context, string environment = "Development") =>
        new HttpCurrentUser(new HttpContextAccessor { HttpContext = context }, TestSupport.Environment(environment));

    private static DefaultHttpContext Request(string? claimType = null, string? claimValue = null, string? header = null)
    {
        var context = new DefaultHttpContext();
        if (claimType is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claimType, claimValue!)], "test"));
        }

        if (header is not null)
        {
            context.Request.Headers[HttpCurrentUser.DevelopmentHeader] = header;
        }

        return context;
    }

    [Theory]
    [InlineData("sub")]
    [InlineData(ClaimTypes.NameIdentifier)]
    public void UserId_FromClaim_IsUsed(string claimType)
    {
        Assert.Equal(UserA, CurrentUser(Request(claimType, UserA.ToString())).UserId);
    }

    [Fact]
    public void UserId_ClaimAndHeader_ClaimWins()
    {
        var context = Request("sub", UserA.ToString(), header: UserB.ToString());

        Assert.Equal(UserA, CurrentUser(context).UserId);
    }

    [Fact]
    public void UserId_HeaderInDevelopment_IsUsed()
    {
        Assert.Equal(UserB, CurrentUser(Request(header: UserB.ToString())).UserId);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void UserId_HeaderOutsideDevelopment_IsIgnored(string environment)
    {
        Assert.Null(CurrentUser(Request(header: UserB.ToString()), environment).UserId);
    }

    [Fact]
    public void UserId_InvalidClaimFallsBackToHeader()
    {
        Assert.Equal(UserB, CurrentUser(Request("sub", "not-a-guid", header: UserB.ToString())).UserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public void UserId_NoUsableIdentity_IsNull(string? header)
    {
        Assert.Null(CurrentUser(Request(header: header)).UserId);
    }

    [Fact]
    public void UserId_OutsideARequest_IsNull()
    {
        Assert.Null(CurrentUser(context: null).UserId);
    }

    [Fact]
    public void RequireUserId_SignedIn_ReturnsId()
    {
        Assert.Equal(UserA, CurrentUser(Request("sub", UserA.ToString())).RequireUserId());
    }

    [Fact]
    public void RequireUserId_NotSignedIn_ThrowsUnauthenticated()
    {
        var ex = Assert.Throws<MortgageUnauthenticatedException>(() => CurrentUser(Request()).RequireUserId());

        Assert.Equal("UNAUTHENTICATED", ex.Code);
    }

    [Fact]
    public async Task KycGate_Placeholder_TreatsEveryoneAsVerified()
    {
        Assert.True(await new AlwaysVerifiedKycGate().IsVerifiedAsync(UserA, CancellationToken.None));
    }
}
