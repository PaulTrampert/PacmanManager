using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PacmanManager.RepoHost.Exceptions;

namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// Translates the ways an upload can be refused into HTTP status codes, the way
/// <see cref="AuthorizationExceptionHandler"/> does for the authorization outcomes.
/// </summary>
/// <remarks>
/// <para>
/// The publish path deliberately knows nothing about HTTP, so it says what went wrong with an
/// exception and this decides what that is worth. Three things can go wrong that are not a
/// <c>500</c>:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <see cref="InvalidPackageException"/> — every statement this makes is about the file the
///     caller sent, so it is a <c>400</c>.
///   </description></item>
///   <item><description>
///     <see cref="RepositoryDatabaseLockedException"/> — the repository database was locked by
///     another writer. Nothing happened and retrying is the answer, which is a <c>409</c>.
///   </description></item>
///   <item><description>
///     A <see cref="BadHttpRequestException"/> carrying <c>413</c>, which is how Kestrel reports a
///     body over the configured ceiling once the service has started reading it.
///   </description></item>
/// </list>
/// <para>
/// Everything else is left unhandled and surfaces as a <c>500</c>, including
/// <see cref="RepositoryDatabaseToolException"/>: a tool that failed for its own reasons is a
/// server problem, not something the caller can fix by sending a different request.
/// </para>
/// </remarks>
/// <param name="problemDetailsService">Writes the response body.</param>
public class PackagePublishExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            InvalidPackageException => (StatusCodes.Status400BadRequest, "The uploaded package was not accepted."),
            RepositoryDatabaseLockedException => (StatusCodes.Status409Conflict,
                "The repository database is busy."),
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                (StatusCodes.Status413PayloadTooLarge, "The uploaded package is too large."),
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
