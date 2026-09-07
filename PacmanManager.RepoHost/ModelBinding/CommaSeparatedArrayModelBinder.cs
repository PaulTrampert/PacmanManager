using System.ComponentModel;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PacmanManager.RepoHost.ModelBinding;

/// <summary>
/// Binds a collection-valued parameter from a query string that may repeat the parameter, list its
/// values comma-separated, or do both at once.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core binds <c>?ids=a&amp;ids=b</c> to a collection out of the box, but binds
/// <c>?ids=a,b</c> to a single element holding the literal text <c>a,b</c>, which then fails to
/// convert for any element type that is not a string. This binder accepts both forms, and the
/// mixed form <c>?ids=a,b&amp;ids=c</c>, by splitting every supplied value on commas before
/// converting the pieces.
/// </para>
/// <para>
/// Entries that are empty or whitespace once trimmed are discarded rather than converted, so
/// <c>?ids=a,,b</c> and <c>?ids=a, b</c> both yield two entries. A parameter that is present but
/// contributes no entries at all — <c>?ids=</c> — binds to <see langword="null"/>, which makes it
/// indistinguishable from omitting the parameter. That matters because these bind filter
/// properties, where an unset criterion narrows nothing; an empty collection would otherwise have
/// to mean either "no criterion" or "match nothing", and neither reading is what a caller who sent
/// an empty value intends.
/// </para>
/// <para>
/// Apply it with <see cref="FromCommaSeparatedQueryAttribute"/> rather than naming this type at
/// each use site.
/// </para>
/// </remarks>
public class CommaSeparatedArrayModelBinder : IModelBinder
{
    /// <summary>
    /// The character that separates values within a single query string entry.
    /// </summary>
    private const char Separator = ',';

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="bindingContext"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// The bound member is not a collection, or is a collection type that cannot be assigned from
    /// an array of its element type.
    /// </exception>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var elementType = bindingContext.ModelMetadata.ElementType
                          ?? throw new NotSupportedException(
                              $"{nameof(CommaSeparatedArrayModelBinder)} can only bind collection types, but was applied " +
                              $"to '{bindingContext.ModelMetadata.ModelType}'.");

        if (!bindingContext.ModelMetadata.ModelType.IsAssignableFrom(elementType.MakeArrayType()))
        {
            throw new NotSupportedException(
                $"{nameof(CommaSeparatedArrayModelBinder)} binds an array of the element type, which " +
                $"'{bindingContext.ModelMetadata.ModelType}' cannot be assigned from. Declare the member as " +
                $"{elementType.Name}[] or as an interface an array implements, such as IEnumerable<{elementType.Name}>.");
        }

        var values = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (values == ValueProviderResult.None)
        {
            // Absent entirely: leave the member at its default rather than reporting it as bound.
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, values);

        var entries = Split(values);
        if (entries.Count == 0)
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        var converter = TypeDescriptor.GetConverter(elementType);
        var culture = values.Culture ?? CultureInfo.InvariantCulture;
        var result = Array.CreateInstance(elementType, entries.Count);

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            try
            {
                result.SetValue(converter.ConvertFromString(null, culture, entry), i);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                bindingContext.ModelState.TryAddModelError(
                    bindingContext.ModelName,
                    $"The value '{entry}' is not valid.");
                bindingContext.Result = ModelBindingResult.Failed();
                return Task.CompletedTask;
            }
        }

        bindingContext.Result = ModelBindingResult.Success(result);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Splits every supplied value on commas and drops the entries that are empty once trimmed.
    /// </summary>
    /// <param name="values">The raw values the value provider supplied for the member.</param>
    /// <returns>The entries to convert, in the order they were supplied.</returns>
    private static List<string> Split(ValueProviderResult values)
    {
        var entries = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            entries.AddRange(value
                .Split(Separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(entry => !string.IsNullOrWhiteSpace(entry)));
        }

        return entries;
    }
}
