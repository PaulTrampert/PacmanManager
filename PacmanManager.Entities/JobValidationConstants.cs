namespace PacmanManager.Entities;

/// <summary>
/// Limits on the columns of a <see cref="Job"/>.
/// </summary>
public static class JobValidationConstants
{
    /// <summary>
    /// Maximum length of <see cref="Job.MessageType"/>, the full name of a command type.
    /// </summary>
    public const int MessageTypeMaxLength = 512;
}
