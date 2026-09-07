namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Base class for the reasons an uploaded package file cannot be accepted. Every one of them is a
/// statement about the file the caller supplied, so a request that raises one is a client error.
/// </summary>
/// <param name="message">A description of why the package was rejected.</param>
public abstract class InvalidPackageException(string message) : Exception(message);
