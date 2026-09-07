using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using PacmanManager.RepoHost.ModelBinding;

namespace PacmanManager.RepoHost.Test.ModelBinding;

/// <summary>
/// Unit tests for <see cref="CommaSeparatedArrayModelBinder"/>, covering the comma form, the
/// repeated form, the mixture of the two, and the empty and whitespace-only entries a caller can
/// end up sending when a client joins an empty list.
/// </summary>
public class CommaSeparatedArrayModelBinderTests
{
    private const string ModelName = "ids";

    private static readonly Guid First = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Second = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Third = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private CommaSeparatedArrayModelBinder _binder = null!;

    [SetUp]
    public void SetUp()
    {
        _binder = new CommaSeparatedArrayModelBinder();
    }

    [Test]
    public async Task BindModelAsync_CommaSeparatedForm_BindsEveryValue()
    {
        var context = ContextFor<IEnumerable<Guid>>($"?{ModelName}={First},{Second}");

        await _binder.BindModelAsync(context);

        Assert.That(context.Result.IsModelSet, Is.True);
        Assert.That(context.Result.Model, Is.EqualTo(new[] { First, Second }));
    }

    [Test]
    public async Task BindModelAsync_RepeatedForm_BindsEveryValue()
    {
        var context = ContextFor<IEnumerable<Guid>>($"?{ModelName}={First}&{ModelName}={Second}");

        await _binder.BindModelAsync(context);

        Assert.That(context.Result.IsModelSet, Is.True);
        Assert.That(context.Result.Model, Is.EqualTo(new[] { First, Second }));
    }

    [Test]
    public async Task BindModelAsync_MixedForm_BindsEveryValueInOrder()
    {
        var context = ContextFor<IEnumerable<Guid>>($"?{ModelName}={First},{Second}&{ModelName}={Third}");

        await _binder.BindModelAsync(context);

        Assert.That(context.Result.IsModelSet, Is.True);
        Assert.That(context.Result.Model, Is.EqualTo(new[] { First, Second, Third }));
    }

    [Test]
    public async Task BindModelAsync_EmptyEntries_AreDiscarded()
    {
        var context = ContextFor<IEnumerable<Guid>>($"?{ModelName}=,{First},,{Second},&{ModelName}=");

        await _binder.BindModelAsync(context);

        Assert.That(context.Result.IsModelSet, Is.True);
        Assert.That(context.Result.Model, Is.EqualTo(new[] { First, Second }));
        Assert.That(context.ModelState.ErrorCount, Is.Zero);
    }

    [Test]
    public async Task BindModelAsync_WhitespaceAroundEntries_IsTrimmed()
    {
        var context = ContextFor<IEnumerable<Guid>>($"?{ModelName}=%20{First}%20,%20%20,%20{Second}%20");

        await _binder.BindModelAsync(context);

        Assert.That(context.Result.IsModelSet, Is.True);
        Assert.That(context.Result.Model, Is.EqualTo(new[] { First, Second }));
        Assert.That(context.ModelState.ErrorCount, Is.Zero);
    }

    [TestCase("")]
    [TestCase("%20")]
    [TestCase(",")]
    [TestCase("%20,%20")]
    public async Task BindModelAsync_NoUsableEntries_BindsNull(string value)
    {
        var context = ContextFor<IEnumerable<Guid>>($"?{ModelName}={value}");

        await _binder.BindModelAsync(context);

        Assert.That(context.Result.IsModelSet, Is.True);
        Assert.That(context.Result.Model, Is.Null);
        Assert.That(context.ModelState.ErrorCount, Is.Zero);
    }

    [Test]
    public async Task BindModelAsync_ParameterAbsent_LeavesModelUnbound()
    {
        var context = ContextFor<IEnumerable<Guid>>("?other=value");

        await _binder.BindModelAsync(context);

        Assert.That(context.Result.IsModelSet, Is.False);
        Assert.That(context.ModelState.ContainsKey(ModelName), Is.False);
    }

    [Test]
    public async Task BindModelAsync_UnconvertibleEntry_FailsWithAModelError()
    {
        var context = ContextFor<IEnumerable<Guid>>($"?{ModelName}={First},not-a-guid");

        await _binder.BindModelAsync(context);

        Assert.That(context.Result.IsModelSet, Is.False);
        Assert.That(context.ModelState.ErrorCount, Is.EqualTo(1));
        Assert.That(context.ModelState[ModelName]!.Errors.Single().ErrorMessage,
            Is.EqualTo("The value 'not-a-guid' is not valid."));
    }

    [Test]
    public async Task BindModelAsync_ArrayMember_BindsTheSameWayAsAnEnumerableOne()
    {
        var context = ContextFor<Guid[]>($"?{ModelName}={First},{Second}");

        await _binder.BindModelAsync(context);

        Assert.That(context.Result.IsModelSet, Is.True);
        Assert.That(context.Result.Model, Is.InstanceOf<Guid[]>());
        Assert.That(context.Result.Model, Is.EqualTo(new[] { First, Second }));
    }

    [Test]
    public async Task BindModelAsync_StringElements_BindsWithoutConversion()
    {
        var context = ContextFor<IEnumerable<string>>($"?{ModelName}=x86_64,%20any");

        await _binder.BindModelAsync(context);

        Assert.That(context.Result.IsModelSet, Is.True);
        Assert.That(context.Result.Model, Is.EqualTo(new[] { "x86_64", "any" }));
    }

    [Test]
    public async Task BindModelAsync_IntegerElements_ConvertsEachEntry()
    {
        var context = ContextFor<IEnumerable<int>>($"?{ModelName}=1,2&{ModelName}=3");

        await _binder.BindModelAsync(context);

        Assert.That(context.Result.IsModelSet, Is.True);
        Assert.That(context.Result.Model, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void BindModelAsync_NonCollectionMember_Throws()
    {
        var context = ContextFor<Guid>($"?{ModelName}={First}");

        Assert.That(async () => await _binder.BindModelAsync(context), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void BindModelAsync_CollectionAnArrayCannotBeAssignedTo_Throws()
    {
        var context = ContextFor<List<Guid>>($"?{ModelName}={First}");

        Assert.That(async () => await _binder.BindModelAsync(context), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void BindModelAsync_NullContext_Throws()
    {
        Assert.That(async () => await _binder.BindModelAsync(null!), Throws.TypeOf<ArgumentNullException>());
    }

    /// <summary>
    /// Builds a binding context for a member of type <typeparamref name="T"/> named
    /// <see cref="ModelName"/>, reading from the supplied query string.
    /// </summary>
    /// <typeparam name="T">The type of the member being bound.</typeparam>
    /// <param name="queryString">The raw query string, percent-encoded as a client would send it.</param>
    /// <returns>A binding context ready to hand to the binder.</returns>
    private static DefaultModelBindingContext ContextFor<T>(string queryString)
    {
        var query = new QueryCollection(QueryHelpers.ParseQuery(queryString));
        return new DefaultModelBindingContext
        {
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(T)),
            ModelName = ModelName,
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(BindingSource.Query, query, CultureInfo.InvariantCulture),
        };
    }
}
