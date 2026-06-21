namespace PacmanManager.Entities;

public static class UserValidationConstants
{
    public const int DisplayNameMaxLength = 255;
    public const int EmailMaxLength = 255;
    public const string EmailRegex = @$"^[^@]+@({CommonValidationConstants.DomainNameRegex})";
}