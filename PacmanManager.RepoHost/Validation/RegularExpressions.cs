namespace PacmanManager.RepoHost.Validation;

public class RegularExpressions
{
    public const string RepositoryName = @"^[a-zA-Z0-9@_\+]([-\.]?[a-zA-Z0-9@_\+])*$";
    public const string PackageName = RepositoryName;
}