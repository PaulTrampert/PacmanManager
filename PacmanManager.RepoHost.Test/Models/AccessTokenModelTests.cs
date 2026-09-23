using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test.Models;

/// <summary>
/// Tests for the access token wire models, filter and sort field.
/// </summary>
[TestFixture]
public class AccessTokenModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Request_WithoutAnExpiry_IsValid()
    {
        Assert.That(Validate(new CreateAccessTokenRequest { Name = "laptop" }), Is.Empty);
    }

    [Test]
    public void Request_WithAFutureExpiry_IsValid()
    {
        Assert.That(Validate(new CreateAccessTokenRequest { Name = "laptop", ExpiresAt = Now.AddSeconds(1) }), Is.Empty);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Request_WithAnExpiryNotInTheFuture_IsInvalid(int secondsFromNow)
    {
        var errors = Validate(new CreateAccessTokenRequest { Name = "laptop", ExpiresAt = Now.AddSeconds(secondsFromNow) });

        Assert.That(errors.SelectMany(e => e.MemberNames), Is.EqualTo(new[] { nameof(CreateAccessTokenRequest.ExpiresAt) }));
    }

    [Test]
    public void Request_WithAnEmptyName_IsInvalid()
    {
        Assert.That(Validate(new CreateAccessTokenRequest { Name = "" }), Is.Not.Empty);
    }

    [Test]
    public void Request_WithANameOverTheMaximumLength_IsInvalid()
    {
        var name = new string('a', AccessTokenValidationConstants.NameMaxLength + 1);

        Assert.That(Validate(new CreateAccessTokenRequest { Name = name }), Is.Not.Empty);
    }

    [Test]
    [SetCulture("tr-TR")]
    public void Filter_LowersTheNameTermWithTheInvariantCulture()
    {
        var filter = new AccessTokenFilter { NameContains = "TITLE" };

        Assert.That(filter.NameContains, Is.EqualTo("title"));
    }

    [Test]
    public void Filter_LeavesAnOmittedNameTermOmitted()
    {
        Assert.That(new AccessTokenFilter().NameContains, Is.Null);
    }

    [TestCase(AccessTokenSortField.CreatedAt, SortDirection.Descending)]
    [TestCase(AccessTokenSortField.Name, SortDirection.Ascending)]
    [TestCase(AccessTokenSortField.ExpiresAt, SortDirection.Descending)]
    [TestCase(AccessTokenSortField.LastUsedAt, SortDirection.Descending)]
    public void SortField_DefaultDirections(AccessTokenSortField field, SortDirection expected)
    {
        Assert.That(SortFieldDefaults<AccessTokenSortField>.For(field), Is.EqualTo(expected));
    }

    [Test]
    public void SortField_DefaultsToCreatedAt()
    {
        Assert.That(new SortOptions<AccessTokenSortField>().SortBy, Is.EqualTo(AccessTokenSortField.CreatedAt));
    }

    [Test]
    public void AccessToken_SerializesItsUsername_DerivedFromItsId()
    {
        var token = new AccessToken { Id = Guid.CreateVersion7(), Name = "laptop" };

        var json = JsonSerializer.Serialize(token, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.That(json, Does.Contain($"\"username\":\"{AccessTokenFormat.FormatUsername(token.Id)}\""));
    }

    [Test]
    public void CreatedAccessToken_IsNotAnAccessToken()
    {
        // Were it one, it could be passed where a listing entry is expected, secret and all.
        Assert.That(typeof(AccessToken).IsAssignableFrom(typeof(CreatedAccessToken)), Is.False);
    }

    private static List<ValidationResult> Validate(CreateAccessTokenRequest request)
    {
        var context = new ValidationContext(request, new ClockServices(Now), items: null);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    private class ClockServices(DateTimeOffset now) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(TimeProvider) ? new FixedTimeProvider(now) : null;
    }

    private class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
