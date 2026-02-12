namespace PacmanManager.Entities;

public static class PacmanPackageValidationConstants
{
    public const int NameMaxLength = 255;
    public const string NameRegexPattern = @"[a-z\d_@][a-z\d_-\+@\.]+";
    
    public const int VersionMaxLength = 255;
    public const string VersionRegexPattern = @"(\d+:)?[a-zA-Z\d_\.\+]+(-\d+)?";
    
    public const int ArchitectureMaxLength = 255;
}