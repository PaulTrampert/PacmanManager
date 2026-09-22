namespace PacmanManager.Entities;

public static class UserValidationConstants
{
    public const int DisplayNameMaxLength = 255;
    /// <summary>
    /// Maximum length of a user's normalized display name. It is the display name lowered, so it
    /// shares <see cref="DisplayNameMaxLength"/>.
    /// </summary>
    public const int NormalizedDisplayNameMaxLength = DisplayNameMaxLength;

    public const int EmailMaxLength = 255;
    public const string EmailRegex = @$"^[^@]+@({CommonValidationConstants.DomainNameRegex})";
}