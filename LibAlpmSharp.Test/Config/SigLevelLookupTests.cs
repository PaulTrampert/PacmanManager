using LibAlpmSharp.Config;
using LibAlpmSharp.Interop;

namespace LibAlpmSharp.Test.Config;

public class SigLevelLookupTests
{
    [Test]
    public void LookupSigLevel_EmptyList_ReturnsDefault()
    {
        // Arrange
        var sigLevels = new List<string>();

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That(result, Is.EqualTo(AlpmSigLevel.ALPM_SIG_USE_DEFAULT));
    }

    [Test]
    public void LookupSigLevel_Never_ReturnsZero()
    {
        // Arrange
        var sigLevels = new List<string> { "Never" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That(result, Is.EqualTo((AlpmSigLevel)0));
    }

    [Test]
    public void LookupSigLevel_Optional_SetsOptionalFlags()
    {
        // Arrange
        var sigLevels = new List<string> { "Optional" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That(result, Is.EqualTo(AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL | AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL));
    }

    [Test]
    public void LookupSigLevel_Required_SetsRequiredFlags()
    {
        // Arrange
        var sigLevels = new List<string> { "Required" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That(result, Is.EqualTo(AlpmSigLevel.ALPM_SIG_DATABASE | AlpmSigLevel.ALPM_SIG_PACKAGE));
    }

    [Test]
    public void LookupSigLevel_TrustedOnly_ClearsMarginalFlags()
    {
        // Arrange
        var sigLevels = new List<string> { "Required", "TrustAll", "TrustedOnly" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK), Is.EqualTo((AlpmSigLevel)0));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK), Is.EqualTo((AlpmSigLevel)0));
    }

    [Test]
    public void LookupSigLevel_TrustAll_SetsMarginalFlags()
    {
        // Arrange
        var sigLevels = new List<string> { "Required", "TrustAll" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK), Is.EqualTo(AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK), Is.EqualTo(AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK));
    }

    [Test]
    public void LookupSigLevel_PackageOptional_SetsPackageOptionalFlag()
    {
        // Arrange
        var sigLevels = new List<string> { "PackageOptional" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL), Is.EqualTo(AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE), Is.EqualTo((AlpmSigLevel)0));
    }

    [Test]
    public void LookupSigLevel_PackageRequired_SetsPackageRequiredFlag()
    {
        // Arrange
        var sigLevels = new List<string> { "PackageRequired" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE), Is.EqualTo(AlpmSigLevel.ALPM_SIG_PACKAGE));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL), Is.EqualTo((AlpmSigLevel)0));
    }

    [Test]
    public void LookupSigLevel_PackageTrustedOnly_ClearsPackageMarginalFlag()
    {
        // Arrange
        var sigLevels = new List<string> { "PackageRequired", "PackageTrustAll", "PackageTrustedOnly" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK), Is.EqualTo((AlpmSigLevel)0));
    }

    [Test]
    public void LookupSigLevel_PackageTrustAll_SetsPackageMarginalFlag()
    {
        // Arrange
        var sigLevels = new List<string> { "PackageRequired", "PackageTrustAll" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK), Is.EqualTo(AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK));
    }

    [Test]
    public void LookupSigLevel_DatabaseOptional_SetsDatabaseOptionalFlag()
    {
        // Arrange
        var sigLevels = new List<string> { "DatabaseOptional" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL), Is.EqualTo(AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE), Is.EqualTo((AlpmSigLevel)0));
    }

    [Test]
    public void LookupSigLevel_DatabaseRequired_SetsDatabaseRequiredFlag()
    {
        // Arrange
        var sigLevels = new List<string> { "DatabaseRequired" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE), Is.EqualTo(AlpmSigLevel.ALPM_SIG_DATABASE));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL), Is.EqualTo((AlpmSigLevel)0));
    }

    [Test]
    public void LookupSigLevel_DatabaseTrustedOnly_ClearsDatabaseMarginalFlag()
    {
        // Arrange
        var sigLevels = new List<string> { "DatabaseRequired", "DatabaseTrustAll", "DatabaseTrustedOnly" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK), Is.EqualTo((AlpmSigLevel)0));
    }

    [Test]
    public void LookupSigLevel_DatabaseTrustAll_SetsDatabaseMarginalFlag()
    {
        // Arrange
        var sigLevels = new List<string> { "DatabaseRequired", "DatabaseTrustAll" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK), Is.EqualTo(AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK));
    }

    [Test]
    public void LookupSigLevel_CaseSensitive_ThrowsOnMismatch()
    {
        // Arrange - uppercase should not match lowercase case statements
        var sigLevels = new List<string> { "REQUIRED" };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => SigLevelLookup.LookupSigLevel(sigLevels));
        Assert.That(ex.Message, Does.Contain("Unknown SigLevel value: REQUIRED"));
    }

    [Test]
    public void LookupSigLevel_MultipleCombinations_WorksCorrectly()
    {
        // Arrange
        var sigLevels = new List<string> { "Required", "DatabaseOptional", "PackageTrustAll" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL), Is.EqualTo(AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE), Is.EqualTo(AlpmSigLevel.ALPM_SIG_PACKAGE));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK), Is.EqualTo(AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK));
    }

    [Test]
    public void LookupSigLevel_DefaultIsCleared_AfterFirstValue()
    {
        // Arrange
        var sigLevels = new List<string> { "Required" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_USE_DEFAULT), Is.EqualTo((AlpmSigLevel)0));
    }

    [Test]
    public void LookupSigLevel_InvalidValue_ThrowsArgumentException()
    {
        // Arrange
        var sigLevels = new List<string> { "InvalidValue" };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => SigLevelLookup.LookupSigLevel(sigLevels));
        Assert.That(ex.Message, Does.Contain("Unknown SigLevel value: InvalidValue"));
    }

    [Test]
    public void LookupSigLevel_NeverOverridesEverything()
    {
        // Arrange
        var sigLevels = new List<string> { "Required", "TrustAll", "Never" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That(result, Is.EqualTo((AlpmSigLevel)0));
    }

    [Test]
    public void LookupSigLevel_ComplexScenario_RequiredDatabaseOptionalPackageTrusted()
    {
        // Arrange
        // This is a common scenario: Required signatures, but database is optional
        // and only trusted packages are allowed
        var sigLevels = new List<string> { "Required", "DatabaseOptional", "PackageTrustedOnly" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL), Is.EqualTo(AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE), Is.EqualTo((AlpmSigLevel)0));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE), Is.EqualTo(AlpmSigLevel.ALPM_SIG_PACKAGE));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK), Is.EqualTo((AlpmSigLevel)0));
    }

    [Test]
    public void LookupSigLevel_OptionalThenRequired_LastOneWins()
    {
        // Arrange
        var sigLevels = new List<string> { "Optional", "Required" };

        // Act
        var result = SigLevelLookup.LookupSigLevel(sigLevels);

        // Assert
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE), Is.EqualTo(AlpmSigLevel.ALPM_SIG_DATABASE));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE), Is.EqualTo(AlpmSigLevel.ALPM_SIG_PACKAGE));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL), Is.EqualTo((AlpmSigLevel)0));
        Assert.That((result & AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL), Is.EqualTo((AlpmSigLevel)0));
    }
}
