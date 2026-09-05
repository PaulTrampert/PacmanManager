using PacmanManager.RepoHost.Authentication;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Derives the current <see cref="Actor"/> from the authenticated principal on the HTTP request.
/// </summary>
/// <param name="currentUserService">Resolves the application user behind the request.</param>
public class HttpContextActorAccessor(ICurrentUserService currentUserService) : IActorAccessor
{
    private Actor? _cached;

    /// <inheritdoc />
    public async ValueTask<Actor> GetActorAsync(CancellationToken ct = default)
    {
        // This service is scoped to the request, so the lookup only needs to happen once even
        // though a single operation may consult the actor several times.
        if (_cached is not null)
        {
            return _cached;
        }

        var user = await currentUserService.GetCurrentUserAsync(ct);
        return _cached = user is null ? Actor.Anonymous : Actor.For(user);
    }
}
