using Microsoft.AspNetCore.Mvc.ModelBinding;
using PacmanManager.RepoHost.ModelBinding;

namespace PacmanManager.RepoHost.Test.ModelBinding;

/// <summary>
/// Unit tests for <see cref="FromCommaSeparatedQueryAttribute"/>, which is the whole of the wiring
/// between a filter property and <see cref="CommaSeparatedArrayModelBinder"/>.
/// </summary>
public class FromCommaSeparatedQueryAttributeTests
{
    [Test]
    public void SelectsTheCommaSeparatedBinder()
    {
        Assert.That(new FromCommaSeparatedQueryAttribute().BinderType,
            Is.EqualTo(typeof(CommaSeparatedArrayModelBinder)));
    }

    [Test]
    public void KeepsTheQueryStringAsItsBindingSource()
    {
        // Left to ModelBinderAttribute's own inference this would be BindingSource.Custom, which
        // would let the member bind from anywhere and would stop describing it as a query
        // parameter.
        Assert.That(new FromCommaSeparatedQueryAttribute().BindingSource, Is.EqualTo(BindingSource.Query));
    }
}
