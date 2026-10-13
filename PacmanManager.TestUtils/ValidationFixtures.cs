namespace PacmanManager.TestUtils;

/// <summary>
/// Locates the committed validation fixtures under <c>test-fixtures/validation</c>: the cases a
/// validation rule must accept and reject, shared by RepoHost's tests and the web app's.
/// </summary>
public static class ValidationFixtures
{
    /// <summary>
    /// The directory holding the committed validation fixtures.
    /// </summary>
    public static string Directory =>
        Path.Combine(DirUtils.FindSolutionDirectory(), "test-fixtures", "validation");

    /// <summary>
    /// The absolute path to the repository-name cases, a JSON object of the form
    /// <c>{ "accepted": [...], "rejected": [...] }</c>.
    /// </summary>
    public static string RepositoryNamesPath => Path.Combine(Directory, "repository-names.json");
}
