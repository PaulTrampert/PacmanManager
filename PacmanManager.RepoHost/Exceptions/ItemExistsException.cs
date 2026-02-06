namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Exception thrown when attempting to create an item that already exists.
/// </summary>
public class ItemExistsException : Exception
{
    public ItemExistsException()
    {
    }

    public ItemExistsException(string message) : base(message)
    {
    }

    public ItemExistsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
