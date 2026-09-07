namespace PacmanManager.RepoHost.Controllers;

/// <summary>
/// Route templates shared by the API controllers, so that versioning and resource naming are
/// spelled once rather than once per controller.
/// </summary>
public class ControllerConstants
{
    /// <summary>
    /// The route a controller's own resource is served at, named after the controller and versioned
    /// by namespace.
    /// </summary>
    public const string ControllerBaseRoute = "/api/v{version:apiVersion}/[controller]";

    /// <summary>
    /// The route a repository's packages are addressed under, as an alias for the package routes
    /// keyed by package id.
    /// </summary>
    /// <remarks>
    /// It is an absolute template rather than a suffix on <see cref="ControllerBaseRoute"/> because
    /// it hangs off a different resource: a repository's packages are addressed under that
    /// repository, while the actions serving them belong to the packages controller. Spelling the
    /// version segment out keeps <c>SubstituteApiVersionInUrl</c> working, so Swagger still
    /// documents it as <c>/api/v1/…</c>.
    /// </remarks>
    public const string RepositoryScopedPackagesRoute =
        "/api/v{version:apiVersion}/repositories/{repositoryId:guid}/packages";
}
