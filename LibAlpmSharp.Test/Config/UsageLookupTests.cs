using LibAlpmSharp.Config;
using LibAlpmSharp.Interop;

namespace LibAlpmSharp.Test.Config;

public class UsageLookupTests
{
    [Test]
    public void LookupUsage_EmptyList_ReturnsAll()
    {
        // Arrange
        var usages = new List<string>();

        // Act
        var result = UsageLookup.LookupUsage(usages);

        // Assert
        Assert.That(result, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_ALL));
    }

    [Test]
    public void LookupUsage_Sync_ReturnsSyncFlag()
    {
        // Arrange
        var usages = new List<string> { "Sync" };

        // Act
        var result = UsageLookup.LookupUsage(usages);

        // Assert
        Assert.That(result, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_SYNC));
    }

    [Test]
    public void LookupUsage_Search_ReturnsSearchFlag()
    {
        // Arrange
        var usages = new List<string> { "Search" };

        // Act
        var result = UsageLookup.LookupUsage(usages);

        // Assert
        Assert.That(result, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_SEARCH));
    }

    [Test]
    public void LookupUsage_Install_ReturnsInstallFlag()
    {
        // Arrange
        var usages = new List<string> { "Install" };

        // Act
        var result = UsageLookup.LookupUsage(usages);

        // Assert
        Assert.That(result, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_INSTALL));
    }

    [Test]
    public void LookupUsage_Upgrade_ReturnsUpgradeFlag()
    {
        // Arrange
        var usages = new List<string> { "Upgrade" };

        // Act
        var result = UsageLookup.LookupUsage(usages);

        // Assert
        Assert.That(result, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_UPGRADE));
    }

    [Test]
    public void LookupUsage_All_ReturnsAllFlag()
    {
        // Arrange
        var usages = new List<string> { "All" };

        // Act
        var result = UsageLookup.LookupUsage(usages);

        // Assert
        Assert.That(result, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_ALL));
    }

    [Test]
    public void LookupUsage_MultipleSingleValues_CombinesFlags()
    {
        // Arrange - Multiple values should be ORed together
        var usages = new List<string> { "Sync", "Search" };

        // Act
        var result = UsageLookup.LookupUsage(usages);

        // Assert
        Assert.That(result, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_SYNC | AlpmDbUsage.ALPM_DB_USAGE_SEARCH));
    }

    [Test]
    public void LookupUsage_SyncThenAll_CombinesFlags()
    {
        // Arrange - All flags should be combined with OR
        var usages = new List<string> { "Sync", "All" };

        // Act
        var result = UsageLookup.LookupUsage(usages);

        // Assert - Sync OR All = All (since All includes everything)
        Assert.That(result, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_ALL));
    }

    [Test]
    public void LookupUsage_AllThenSync_ReturnsAll()
    {
        // Arrange - All flags should be combined with OR
        var usages = new List<string> { "All", "Sync" };

        // Act
        var result = UsageLookup.LookupUsage(usages);

        // Assert - All OR Sync = All (since All includes everything)
        Assert.That(result, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_ALL));
    }

    [Test]
    public void LookupUsage_InvalidValue_ThrowsArgumentException()
    {
        // Arrange
        var usages = new List<string> { "InvalidUsage" };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => UsageLookup.LookupUsage(usages));
        Assert.That(ex.Message, Does.Contain("Invalid usage type: InvalidUsage"));
    }

    [Test]
    public void LookupUsage_CaseSensitive_ThrowsOnMismatch()
    {
        // Arrange - lowercase should not match
        var usages = new List<string> { "sync" };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => UsageLookup.LookupUsage(usages));
        Assert.That(ex.Message, Does.Contain("Invalid usage type: sync"));
    }

    [Test]
    public void LookupUsage_UpperCaseSYNC_ThrowsArgumentException()
    {
        // Arrange - uppercase should not match
        var usages = new List<string> { "SYNC" };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => UsageLookup.LookupUsage(usages));
        Assert.That(ex.Message, Does.Contain("Invalid usage type: SYNC"));
    }

    [Test]
    public void LookupUsage_ValidThenInvalid_ThrowsOnInvalid()
    {
        // Arrange
        var usages = new List<string> { "Sync", "Invalid" };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => UsageLookup.LookupUsage(usages));
        Assert.That(ex.Message, Does.Contain("Invalid usage type: Invalid"));
    }

    [Test]
    public void LookupUsage_ThreeValues_CombinesAllFlags()
    {
        // Arrange - All values should be ORed together
        var usages = new List<string> { "Sync", "Search", "Install" };

        // Act
        var result = UsageLookup.LookupUsage(usages);

        // Assert
        var expected = AlpmDbUsage.ALPM_DB_USAGE_SYNC | AlpmDbUsage.ALPM_DB_USAGE_SEARCH | AlpmDbUsage.ALPM_DB_USAGE_INSTALL;
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void LookupUsage_UsesOrOperator_CombinesFlags()
    {
        // Arrange - Each value should be ORed together
        var usages = new List<string> { "Sync", "Upgrade" };

        // Act
        var result = UsageLookup.LookupUsage(usages);

        // Assert
        var expected = AlpmDbUsage.ALPM_DB_USAGE_SYNC | AlpmDbUsage.ALPM_DB_USAGE_UPGRADE;
        Assert.That(result, Is.EqualTo(expected));
        Assert.That((result & AlpmDbUsage.ALPM_DB_USAGE_SYNC), Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_SYNC));
        Assert.That((result & AlpmDbUsage.ALPM_DB_USAGE_UPGRADE), Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_UPGRADE));
    }
}
