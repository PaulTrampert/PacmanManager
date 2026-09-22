using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Test.Entities;

/// <summary>
/// Pins the parts of the <see cref="PacmanAccessToken"/> model that later work relies on: the
/// per-user, case-insensitive name uniqueness and the cascade from <see cref="User"/>. The model is
/// built by the in-memory provider, which reads the same data annotations the migration was
/// generated from.
/// </summary>
[TestFixture]
public class PacmanAccessTokenModelTests
{
    private IEntityType _entityType;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var options = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(nameof(PacmanAccessTokenModelTests))
            .Options;
        using var dbContext = new PacmanManagerDbContext(options);
        _entityType = dbContext.Model.FindEntityType(typeof(PacmanAccessToken))!;
    }

    [Test]
    public void Model_HasUniqueIndexOnUserIdAndNormalizedName()
    {
        var index = _entityType.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(PacmanAccessToken.UserId), nameof(PacmanAccessToken.NormalizedName)]));

        Assert.That(index, Is.Not.Null);
        Assert.That(index!.IsUnique, Is.True);
    }

    [Test]
    public void Model_HasNoUniqueIndexOnName()
    {
        var uniqueIndexesOnName = _entityType.GetIndexes()
            .Where(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(PacmanAccessToken.Name)));

        Assert.That(uniqueIndexesOnName, Is.Empty);
    }

    [Test]
    public void Model_UserForeignKey_IsRequiredAndCascades()
    {
        var foreignKey = _entityType.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(User));

        Assert.Multiple(() =>
        {
            Assert.That(foreignKey.Properties.Select(p => p.Name),
                Is.EqualTo(new[] { nameof(PacmanAccessToken.UserId) }));
            Assert.That(foreignKey.IsRequired, Is.True);
            Assert.That(foreignKey.DeleteBehavior, Is.EqualTo(DeleteBehavior.Cascade));
        });
    }

    [Test]
    public void Model_NormalizedName_SharesNamesMaximumLength()
    {
        var name = _entityType.FindProperty(nameof(PacmanAccessToken.Name))!;
        var normalizedName = _entityType.FindProperty(nameof(PacmanAccessToken.NormalizedName))!;

        Assert.Multiple(() =>
        {
            Assert.That(name.GetMaxLength(), Is.EqualTo(AccessTokenValidationConstants.NameMaxLength));
            Assert.That(normalizedName.GetMaxLength(), Is.EqualTo(name.GetMaxLength()));
        });
    }

    [Test]
    public void Model_TokenHash_FitsABase64Sha256()
    {
        var tokenHash = _entityType.FindProperty(nameof(PacmanAccessToken.TokenHash))!;
        var base64Sha256 = Convert.ToBase64String(new byte[32]);

        Assert.That(tokenHash.GetMaxLength(), Is.EqualTo(base64Sha256.Length));
    }

    [Test]
    public void Model_ExpiryAndLastUse_AreOptional()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_entityType.FindProperty(nameof(PacmanAccessToken.ExpiresAt))!.IsNullable, Is.True);
            Assert.That(_entityType.FindProperty(nameof(PacmanAccessToken.LastUsedAt))!.IsNullable, Is.True);
        });
    }
}
