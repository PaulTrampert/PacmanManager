namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Base class for the reasons an uploaded package file cannot be accepted. Every one of them is a
/// statement about the file the caller supplied, so a request that raises one is a client error.
/// </summary>
public abstract class InvalidPackageException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">A description of why the package was rejected.</param>
    protected InvalidPackageException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates the exception from an underlying failure, for the rejections that are something else
    /// failing to make sense of the file.
    /// </summary>
    /// <param name="message">A description of why the package was rejected.</param>
    /// <param name="innerException">The failure that led to the rejection.</param>
    protected InvalidPackageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
