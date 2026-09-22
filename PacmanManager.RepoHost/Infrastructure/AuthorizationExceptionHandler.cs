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
/// <para>
/// <see cref="ItemExistsException"/> maps to <c>409 Conflict</c> with a fixed title and no
/// <see cref="ProblemDetails.Detail"/>. A collision on a repository name must disclose nothing but
/// that the name is taken, and leaving the exception's message out of the body makes that true for
/// every raise site by construction, rather than relying on each one to word its message carefully.
/// </para>
/// </remarks>
/// <param name="problemDetailsService">Writes the response body.</param>
public class AuthorizationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NoCurrentUserException => (StatusCodes.Status401Unauthorized, "Authentication required.", exception.Message),
            RepositoryForbiddenException => (StatusCodes.Status403Forbidden, "You do not own this repository.", exception.Message),
            PackageForbiddenException => (StatusCodes.Status403Forbidden, "You may not publish to this repository.", exception.Message),
            ItemExistsException => (StatusCodes.Status409Conflict, "The item already exists.", null),
            _ => (0, string.Empty, (string?)null),
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
                Detail = detail,
            },
        });
    }
}
