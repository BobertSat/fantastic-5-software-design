using EuroGlobe.Mortgages.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EuroGlobe.Api.Controllers.Mortgages;

/// <summary>Base for mortgage controllers: thin, and every module error becomes a problem response.</summary>
[ApiController]
[MortgageProblemDetails]
public abstract class MortgageControllerBase : ControllerBase;

/// <summary>
/// Maps <see cref="MortgageException"/> types to RFC 7807 responses with a machine-readable <c>code</c>
/// (ADR-M018). Other exceptions are left to the framework (500).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class MortgageProblemDetailsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not MortgageException error)
        {
            return;
        }

        var status = StatusFor(error);
        if (status is null)
        {
            return;
        }

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        ProblemDetails problem;
        if (error is MortgageValidationException validation)
        {
            var modelState = new ModelStateDictionary();
            foreach (var (field, messages) in validation.Errors)
            {
                foreach (var message in messages)
                {
                    modelState.AddModelError(field, message);
                }
            }

            problem = factory.CreateValidationProblemDetails(context.HttpContext, modelState, status, detail: error.Message);
        }
        else
        {
            problem = factory.CreateProblemDetails(context.HttpContext, status, TitleFor(status.Value), detail: error.Message);
        }

        problem.Extensions["code"] = error.Code;
        context.Result = new ObjectResult(problem) { StatusCode = status };
        context.ExceptionHandled = true;
    }

    internal static int? StatusFor(MortgageException error) => error switch
    {
        MortgageValidationException => StatusCodes.Status400BadRequest,
        MortgageUnauthenticatedException => StatusCodes.Status401Unauthorized,
        MortgageAccessDeniedException => StatusCodes.Status403Forbidden,
        MortgageNotFoundException => StatusCodes.Status404NotFound,
        InvalidMortgageStateException => StatusCodes.Status409Conflict,
        MortgageRuleViolationException => StatusCodes.Status422UnprocessableEntity,
        _ => null,
    };

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => "Not signed in",
        StatusCodes.Status403Forbidden => "Not allowed",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Not allowed in the current state",
        _ => "Business rule failed",
    };
}
