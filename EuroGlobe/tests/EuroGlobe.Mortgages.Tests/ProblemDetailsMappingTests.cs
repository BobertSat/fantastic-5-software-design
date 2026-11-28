using EuroGlobe.Api.Controllers.Mortgages;
using EuroGlobe.Mortgages.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EuroGlobe.Mortgages.Tests;

public sealed class ProblemDetailsMappingTests
{
    private static ExceptionContext Run(Exception exception)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore();
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var context = new ExceptionContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [])
        {
            Exception = exception,
        };

        new MortgageProblemDetailsAttribute().OnException(context);
        return context;
    }

    public static TheoryData<MortgageException, int, string> Errors => new()
    {
        { new MortgageUnauthenticatedException(), 401, "UNAUTHENTICATED" },
        { new MortgageAccessDeniedException("Not your account."), 403, "FORBIDDEN" },
        { new MortgageNotFoundException(Guid.Empty), 404, "MORTGAGE_NOT_FOUND" },
        { new InvalidMortgageStateException(MortgageState.Closed, "sign"), 409, "INVALID_MORTGAGE_STATE" },
        { new MortgageRuleViolationException("KYC_NOT_VERIFIED", "KYC is pending."), 422, "KYC_NOT_VERIFIED" },
    };

    [Theory]
    [MemberData(nameof(Errors))]
    public void OnException_MortgageError_ReturnsProblemWithStatusAndCode(MortgageException error, int status, string code)
    {
        var context = Run(error);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(result.Value);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(status, problem.Status);
        Assert.Equal(code, problem.Extensions["code"]);
        Assert.Equal(error.Message, problem.Detail);
        Assert.False(string.IsNullOrEmpty(problem.Title));
    }

    [Fact]
    public void OnException_Validation_Returns400WithFieldErrors()
    {
        var error = new MortgageValidationException(new Dictionary<string, string[]>
        {
            ["amount"] = ["Must be above 0."],
            ["termMonths"] = ["Must be between 60 and 360.", "Must be whole months."],
        });

        var context = Run(error);

        var result = Assert.IsType<ObjectResult>(context.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("VALIDATION_FAILED", problem.Extensions["code"]);
        Assert.Equal(["Must be above 0."], problem.Errors["amount"]);
        Assert.Equal(2, problem.Errors["termMonths"].Length);
    }

    [Fact]
    public void OnException_OtherException_IsLeftForTheFramework()
    {
        var context = Run(new InvalidOperationException("bug"));

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [Fact]
    public void OnException_UnmappedMortgageErrorType_IsLeftForTheFramework()
    {
        var context = Run(new UnmappedError());

        Assert.False(context.ExceptionHandled);
    }

    [Fact]
    public void Exceptions_ExposeTheirDetails()
    {
        var id = Guid.NewGuid();
        var state = new InvalidMortgageStateException(MortgageState.Delinquent, "repay early");

        Assert.Equal(id, new MortgageNotFoundException(id).MortgageId);
        Assert.Equal(MortgageState.Delinquent, state.State);
        Assert.Equal("repay early", state.Action);
        Assert.Equal("Cannot repay early a mortgage that is Delinquent.", state.Message);
    }

    [Fact]
    public void MortgageControllerBase_AppliesTheFilter()
    {
        Assert.True(typeof(MortgageControllerBase).IsDefined(typeof(MortgageProblemDetailsAttribute), inherit: true));
        Assert.True(typeof(MortgageControllerBase).IsDefined(typeof(ApiControllerAttribute), inherit: true));
    }

    private sealed class UnmappedError() : MortgageException("UNMAPPED", "A new error type without a status yet.");
}
