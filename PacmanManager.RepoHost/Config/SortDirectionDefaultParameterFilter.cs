using Microsoft.OpenApi;
using PacmanManager.RepoHost.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PacmanManager.RepoHost.Config;

/// <summary>
/// Appends each listing's per-field default sort direction to the documentation of its
/// <c>direction</c> query parameter.
/// </summary>
/// <remarks>
/// <see cref="SortOptions{TSortFields}.Direction"/> is nullable, so
/// <see cref="System.ComponentModel.DefaultValueAttribute"/> can no longer carry its default — and
/// a single attribute could not express a default that varies by sort field anyway. The text is
/// generated from the same <see cref="DefaultSortDirectionAttribute"/> declarations the ordering
/// itself reads, so the documentation cannot fall out of step with the behaviour, and every
/// listing that binds a <see cref="SortOptions{TSortFields}"/> is documented without doing
/// anything.
/// </remarks>
public class SortDirectionDefaultParameterFilter : IParameterFilter
{
    private const string DirectionPropertyName = nameof(SortOptions<RepositorySortField>.Direction);

    /// <inheritdoc />
    public void Apply(IOpenApiParameter parameter, ParameterFilterContext context)
    {
        // Only the mutable implementation can be annotated; a referenced parameter is not ours.
        if (parameter is not OpenApiParameter target) return;

        var property = context.PropertyInfo;
        if (property?.Name != DirectionPropertyName) return;

        var declaringType = property.DeclaringType;
        if (declaringType is not { IsGenericType: true } ||
            declaringType.GetGenericTypeDefinition() != typeof(SortOptions<>))
        {
            return;
        }

        var sortFieldType = declaringType.GetGenericArguments()[0];
        var describe = typeof(SortFieldDefaults<>)
            .MakeGenericType(sortFieldType)
            .GetMethod(nameof(SortFieldDefaults<RepositorySortField>.Describe));

        if (describe?.Invoke(null, null) is not string defaults) return;

        target.Description = string.IsNullOrWhiteSpace(target.Description)
            ? defaults
            : $"{target.Description.TrimEnd()} {defaults}";
    }
}
