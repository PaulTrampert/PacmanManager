using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;
using PTrampert.SimplePatch;

namespace PacmanManager.RepoHost.Test.Models;

/// <summary>
/// The patch semantics of <c>PATCH /api/v1/users/me</c>: how a body deserializes to an
/// <see cref="IPatchObject{T}"/> of <see cref="WriteUserRequest"/>, how it validates, and what applying
/// it does. These are the reason <c>PTrampert.SimplePatch</c> is a dependency at all.
/// </summary>
[TestFixture]
public class WriteUserRequestPatchTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static readonly CurrentUser Current = new()
    {
        Id = Guid.CreateVersion7(),
        DisplayName = "Alex",
        Email = "alex@example.com",
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        // The same naming policy as the API's own options, plus the converters Program.cs registers.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.AddSimplePatchConverters();
        return options;
    }

    private static IPatchObject<WriteUserRequest> Deserialize(string json) =>
        JsonSerializer.Deserialize<IPatchObject<WriteUserRequest>>(json, JsonOptions)!;

    /// <summary>
    /// Runs every validation attribute on the patch object's properties, read by reflection, as MVC's
    /// model validation does.
    /// </summary>
    /// <remarks>
    /// Not <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}?, bool)"/>:
    /// it reads attributes through <c>TypeDescriptor</c>, which keeps one attribute per
    /// <see cref="Attribute.TypeId"/>. The generated property carries one
    /// <see cref="OptionalValidationAttribute"/> per source attribute, all sharing a type id, so it would
    /// see only one of <c>[Required]</c> and <c>[MaxLength]</c>. MVC sees both.
    /// </remarks>
    private static List<ValidationResult> Validate(IPatchObject<WriteUserRequest> patch)
    {
        var results = new List<ValidationResult>();
        foreach (var property in patch.GetType().GetProperties())
        {
            var context = new ValidationContext(patch) { MemberName = property.Name };
            var value = property.GetValue(patch);
            foreach (var attribute in property.GetCustomAttributes<ValidationAttribute>(inherit: true))
            {
                if (attribute.GetValidationResult(value, context) is { } result && result != ValidationResult.Success)
                {
                    results.Add(result);
                }
            }
        }

        return results;
    }

    [Test]
    public void FromCurrentUser_CopiesTheWritableValues()
    {
        var request = WriteUserRequest.FromCurrentUser(Current);

        Assert.That(request, Is.EqualTo(new WriteUserRequest { DisplayName = "Alex" }));
    }

    [Test]
    public void EmptyBody_IsValid_AndChangesNothing()
    {
        var patch = Deserialize("{}");
        var target = WriteUserRequest.FromCurrentUser(Current);

        var result = patch.Patch(target);

        Assert.Multiple(() =>
        {
            Assert.That(Validate(patch), Is.Empty, "an omitted required property is not a validation error");
            Assert.That(result, Is.EqualTo(target));
        });
    }

    [Test]
    public void BodyNamingDisplayName_IsValid_AndChangesIt()
    {
        var patch = Deserialize("""{"displayName": "Alexandra"}""");

        var result = patch.Patch(WriteUserRequest.FromCurrentUser(Current));

        Assert.Multiple(() =>
        {
            Assert.That(Validate(patch), Is.Empty);
            Assert.That(result.DisplayName, Is.EqualTo("Alexandra"));
        });
    }

    [Test]
    public void Patch_DoesNotMutateItsTarget()
    {
        var patch = Deserialize("""{"displayName": "Alexandra"}""");
        var target = WriteUserRequest.FromCurrentUser(Current);

        patch.Patch(target);

        Assert.That(target.DisplayName, Is.EqualTo("Alex"));
    }

    [Test]
    public void ExplicitNullDisplayName_FailsRequired()
    {
        var patch = Deserialize("""{"displayName": null}""");

        var results = Validate(patch);

        Assert.That(results.SelectMany(r => r.MemberNames), Does.Contain(nameof(WriteUserRequest.DisplayName)));
    }

    [Test]
    public void DisplayNameAtTheMaximumLength_IsValid()
    {
        var name = new string('a', UserValidationConstants.DisplayNameMaxLength);
        var patch = Deserialize(JsonSerializer.Serialize(new { displayName = name }));

        Assert.That(Validate(patch), Is.Empty);
    }

    [Test]
    public void DisplayNameOverTheMaximumLength_FailsMaxLength()
    {
        var name = new string('a', UserValidationConstants.DisplayNameMaxLength + 1);
        var patch = Deserialize(JsonSerializer.Serialize(new { displayName = name }));

        var results = Validate(patch);

        Assert.That(results.SelectMany(r => r.MemberNames), Does.Contain(nameof(WriteUserRequest.DisplayName)));
    }
}
