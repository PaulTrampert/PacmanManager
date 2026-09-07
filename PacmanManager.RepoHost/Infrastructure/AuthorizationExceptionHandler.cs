using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PacmanManager.RepoHost.Exceptions;

namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// Translates the authorization outcomes the service layer signals with exceptions into HTTP
/// status codes.
/// </summary>
/// <remarks>
/// <para>
/// Services deliberately do not know about HTTP, so they raise
/// <see cref="NoCurrentUserException"/>, <see cref="RepositoryForbiddenException"/> and
/// <see cref="PackageForbiddenException"/> instead of returning action results. Mapping them
/// centrally keeps every controller free of the translation and guarantees the same status code
/// from every route.
/// </para>
/// <para>
/// The switch below leaves every other exception unhandled, so an authorization exception without
/// an arm here would surface as a <c>500</c> rather than the status it means.
/// </para>
/// </remarks>
/// <param name="problemDetailsService">Writes the response body.</param>
public class AuthorizationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NoCurrentUserException => (StatusCodes.Status401Unauthorized, "Authentication required."),
            RepositoryForbiddenException => (StatusCodes.Status403Forbidden, "You do not own this repository."),
            PackageForbiddenException => (StatusCodes.Status403Forbidden, "You may not publish to this repository."),
            _ => (0, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message,
            },
        });
    }
}
