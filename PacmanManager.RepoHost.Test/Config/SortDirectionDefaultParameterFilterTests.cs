using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using PacmanManager.RepoHost.Config;
using PacmanManager.RepoHost.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PacmanManager.RepoHost.Test.Config;

/// <summary>
/// Tests that the <c>direction</c> query parameter documents its per-field defaults, which
/// <see cref="System.ComponentModel.DefaultValueAttribute"/> can no longer do now that the
/// property is nullable.
/// </summary>
[TestFixture]
public class SortDirectionDefaultParameterFilterTests
{
    private SortDirectionDefaultParameterFilter _filter = null!;

    [SetUp]
    public void SetUp()
    {
        _filter = new SortDirectionDefaultParameterFilter();
    }

    [Test]
    public void Apply_AppendsThePerFieldDefaults_ToTheDirectionParameter()
    {
        // Arrange
        var parameter = new OpenApiParameter { Name = "direction", Description = "The direction to order in." };

        // Act
        _filter.Apply(parameter, ContextFor(typeof(SortOptions<RepositorySortField>), nameof(SortOptions<RepositorySortField>.Direction)));

        // Assert
        Assert.That(parameter.Description, Is.EqualTo(
            "The direction to order in. When omitted, defaults to Ascending for Name; Descending for Created, Updated."));
    }

    [Test]
    public void Apply_UsesTheDefaultsAloneWhenTheParameterHasNoDescription()
    {
        // Arrange
        var parameter = new OpenApiParameter { Name = "direction" };

        // Act
        _filter.Apply(parameter, ContextFor(typeof(SortOptions<RepositorySortField>), nameof(SortOptions<RepositorySortField>.Direction)));

        // Assert
        Assert.That(parameter.Description, Is.EqualTo(
            "When omitted, defaults to Ascending for Name; Descending for Created, Updated."));
    }

    [Test]
    public void Apply_LeavesOtherParametersAlone()
    {
        // Arrange
        var parameter = new OpenApiParameter { Name = "sortBy", Description = "The property to order by." };

        // Act
        _filter.Apply(parameter, ContextFor(typeof(SortOptions<RepositorySortField>), nameof(SortOptions<RepositorySortField>.SortBy)));

        // Assert
        Assert.That(parameter.Description, Is.EqualTo("The property to order by."));
    }

    [Test]
    public void Apply_LeavesAloneADirectionThatIsNotASortOptionsProperty()
    {
        // The filter matches on the declaring type as well as the name, so an unrelated property
        // that happens to be called Direction is not annotated with a listing's sort defaults.
        // Arrange
        var parameter = new OpenApiParameter { Name = "direction", Description = "Which way the wind blows." };

        // Act
        _filter.Apply(parameter, ContextFor(typeof(Weathervane), nameof(Weathervane.Direction)));

        // Assert
        Assert.That(parameter.Description, Is.EqualTo("Which way the wind blows."));
    }

    private static ParameterFilterContext ContextFor(Type declaringType, string propertyName)
    {
        var property = declaringType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, $"{declaringType.Name}.{propertyName} should exist.");

        // Only PropertyInfo is read by the filter; the generation services are not reached.
        return new ParameterFilterContext(
            new ApiParameterDescription(),
            null!,
            null!,
            null!,
            property,
            null);
    }

    private sealed record Weathervane
    {
        public string? Direction { get; init; }
    }
}
