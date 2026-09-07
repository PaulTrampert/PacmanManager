using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using PacmanManager.RepoHost.ModelBinding;

namespace PacmanManager.RepoHost.Test.ModelBinding;

/// <summary>
/// Binds a filter-shaped record through MVC's own binder factory, so that
/// <see cref="FromCommaSeparatedQueryAttribute"/> is exercised the way a controller parameter
/// exercises it rather than by calling the binder directly.
/// </summary>
/// <remarks>
/// This is what catches a binding source that stops the property from being offered to the binder
/// at all, which a direct call to <see cref="CommaSeparatedArrayModelBinder"/> cannot see.
/// </remarks>
public class CommaSeparatedQueryBindingTests
{
    private static readonly Guid First = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Second = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Third = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>
    /// Stands in for the package filter, which is not this issue's to write. It only needs one
    /// annotated collection property and one ordinary one.
    /// </summary>
    public record TestFilter
    {
        /// <summary>A collection accepting both the comma form and the repeated form.</summary>
        [FromCommaSeparatedQuery]
        public IEnumerable<Guid>? RepositoryIds { get; init; }

        /// <summary>An ordinary property, to show the annotation does not disturb the rest.</summary>
        public string? NameContains { get; init; }
    }

    [Test]
    public async Task CommaSeparatedForm_Binds()
    {
        var filter = await BindAsync($"?repositoryIds={First},{Second}");

        Assert.That(filter!.RepositoryIds, Is.EqualTo(new[] { First, Second }));
    }

    [Test]
    public async Task RepeatedForm_Binds()
    {
        var filter = await BindAsync($"?repositoryIds={First}&repositoryIds={Second}");

        Assert.That(filter!.RepositoryIds, Is.EqualTo(new[] { First, Second }));
    }

    [Test]
    public async Task MixedForm_Binds()
    {
        var filter = await BindAsync($"?repositoryIds={First},{Second}&repositoryIds={Third}");

        Assert.That(filter!.RepositoryIds, Is.EqualTo(new[] { First, Second, Third }));
    }

    [Test]
    public async Task EmptyAndWhitespaceEntries_LeaveTheCriterionUnset()
    {
        var filter = await BindAsync("?repositoryIds=%20,,%20%20&nameContains=tool");

        Assert.That(filter!.RepositoryIds, Is.Null);
        Assert.That(filter.NameContains, Is.EqualTo("tool"));
    }

    [Test]
    public async Task OmittedParameter_LeavesTheCriterionUnset()
    {
        var filter = await BindAsync("?nameContains=tool");

        Assert.That(filter!.RepositoryIds, Is.Null);
        Assert.That(filter.NameContains, Is.EqualTo("tool"));
    }

    /// <summary>
    /// Binds a <see cref="TestFilter"/> from the supplied query string using the binder MVC would
    /// build for a <c>[FromQuery]</c> action parameter.
    /// </summary>
    /// <param name="queryString">The raw query string, percent-encoded as a client would send it.</param>
    /// <returns>The bound filter, or null if MVC did not bind one.</returns>
    private static async Task<TestFilter?> BindAsync(string queryString)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddMvcCore()
            .Services
            .BuildServiceProvider();

        var metadataProvider = services.GetRequiredService<IModelMetadataProvider>();
        var metadata = metadataProvider.GetMetadataForType(typeof(TestFilter));
        var bindingInfo = new BindingInfo { BindingSource = BindingSource.Query };

        var binder = services.GetRequiredService<IModelBinderFactory>().CreateBinder(new ModelBinderFactoryContext
        {
            Metadata = metadata,
            BindingInfo = bindingInfo,
        });

        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = services;
        httpContext.Request.Query = new QueryCollection(QueryHelpers.ParseQuery(queryString));

        var valueProvider = new CompositeValueProvider
        {
            new QueryStringValueProvider(BindingSource.Query, httpContext.Request.Query, CultureInfo.InvariantCulture),
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var bindingContext = DefaultModelBindingContext.CreateBindingContext(
            actionContext, valueProvider, metadata, bindingInfo, string.Empty);

        await binder.BindModelAsync(bindingContext);

        Assert.That(bindingContext.ModelState.ErrorCount, Is.Zero, "binding reported errors");
        return bindingContext.Result.Model as TestFilter;
    }
}
