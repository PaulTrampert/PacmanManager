using PacmanManager.RepoHost.Infrastructure;

namespace PacmanManager.RepoHost.Test.Infrastructure;

/// <summary>
/// Tests for the key <see cref="PostgresRepositoryDatabaseLock"/> derives from a repository's id.
/// Every process has to arrive at the same key for the same repository, or the lock excludes nothing.
/// </summary>
[TestFixture]
public class PostgresRepositoryDatabaseLockKeyTests
{
    [Test]
    public void DeriveRepositoryKey_GivesTheSameKey_ForTheSameIdOnEveryCall()
    {
        // Arrange
        var id = Guid.CreateVersion7();

        // Act
        var keys = Enumerable.Range(0, 5)
            .Select(_ => PostgresRepositoryDatabaseLock.DeriveRepositoryKey(id))
            .ToList();

        // Assert
        Assert.That(keys, Has.All.EqualTo(keys[0]));
    }

    [Test]
    public void DeriveRepositoryKey_IsTheXorOfTheIdsFour32BitWords()
    {
        // Arrange: the words, as Guid lays out its bytes, are 0x01020304, 0x05060708, 0x0A0B0C0D and
        // 0x10203040. A fixed expected value is what shows the key comes from the id alone, and not
        // from anything, such as Guid.GetHashCode, that differs between processes.
        var id = new Guid([
            0x04, 0x03, 0x02, 0x01,
            0x08, 0x07, 0x06, 0x05,
            0x0D, 0x0C, 0x0B, 0x0A,
            0x40, 0x30, 0x20, 0x10,
        ]);

        // Act
        var key = PostgresRepositoryDatabaseLock.DeriveRepositoryKey(id);

        // Assert
        Assert.That(key, Is.EqualTo(0x01020304 ^ 0x05060708 ^ 0x0A0B0C0D ^ 0x10203040));
    }

    [Test]
    public void DeriveRepositoryKey_UsesEveryWord_SoIdsSharingATimestampDiffer()
    {
        // Arrange: two version 7 ids from the same instant share their leading timestamp bytes.
        var first = new Guid("01890a5d-ac96-774b-bcce-b302099a8057");
        var second = new Guid("01890a5d-ac96-774b-bcce-b302099a8058");

        // Act
        var firstKey = PostgresRepositoryDatabaseLock.DeriveRepositoryKey(first);
        var secondKey = PostgresRepositoryDatabaseLock.DeriveRepositoryKey(second);

        // Assert
        Assert.That(firstKey, Is.Not.EqualTo(secondKey));
    }
}
