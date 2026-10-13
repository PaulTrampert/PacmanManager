using System.ComponentModel.DataAnnotations;
using System.Reflection;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Validation;

namespace PacmanManager.RepoHost.Test.Validation;

/// <summary>
/// Every <see cref="RegularExpressionAttribute"/> on RepoHost's request models carries an
/// <see cref="ValidationAttribute.ErrorMessage"/>. A client that cannot check a pattern itself shows
/// the API's message, and DataAnnotations' default quotes the regular expression at the user.
/// </summary>
[TestFixture]
public class RegularExpressionErrorMessageTests
{
    private const BindingFlags Members =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Test]
    public void EveryRegularExpressionInRepoHost_HasAnErrorMessage()
    {
        var types = typeof(WriteRepositoryRequest).Assembly.GetTypes();

        Assert.Multiple(() =>
        {
            Assert.That(RegularExpressionSites(types), Is.Not.Empty,
                "The scan found no [RegularExpression] at all, so it is not looking in the right place.");
            Assert.That(SitesWithoutErrorMessage(types), Is.Empty);
        });
    }

    [Test]
    public void TheScan_ReportsARegularExpressionWithoutAnErrorMessage()
    {
        Assert.That(SitesWithoutErrorMessage([typeof(ModelWithoutMessages)]), Is.EquivalentTo(new[]
        {
            $"{nameof(ModelWithoutMessages)}.{nameof(ModelWithoutMessages.Property)}",
            $"{nameof(ModelWithoutMessages)}.{nameof(ModelWithoutMessages.Action)}(parameter)",
        }));
    }

    [Test]
    public void TheScan_AcceptsARegularExpressionWithAnErrorMessage()
    {
        Assert.That(SitesWithoutErrorMessage([typeof(ModelWithMessages)]), Is.Empty);
    }

    [Test]
    public void ARepositoryNameFailure_ReportsTheHumanMessage()
    {
        var request = new WriteRepositoryRequest { Name = "-bad" };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        Assert.That(results.Select(r => r.ErrorMessage),
            Is.EquivalentTo(new[] { RegularExpressions.RepositoryNameErrorMessage }));
    }

    /// <summary>
    /// Each property and method parameter among <paramref name="types"/> that carries a
    /// <see cref="RegularExpressionAttribute"/>, named <c>Type.Property</c> or
    /// <c>Type.Method(parameter)</c>, with the attribute.
    /// </summary>
    private static IEnumerable<(string Site, RegularExpressionAttribute Attribute)> RegularExpressionSites(
        IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            foreach (var property in type.GetProperties(Members))
            {
                foreach (var attribute in property.GetCustomAttributes<RegularExpressionAttribute>())
                {
                    yield return ($"{type.Name}.{property.Name}", attribute);
                }
            }

            foreach (var method in type.GetMethods(Members))
            {
                foreach (var parameter in method.GetParameters())
                {
                    foreach (var attribute in parameter.GetCustomAttributes<RegularExpressionAttribute>())
                    {
                        yield return ($"{type.Name}.{method.Name}({parameter.Name})", attribute);
                    }
                }
            }
        }
    }

    private static IEnumerable<string> SitesWithoutErrorMessage(IEnumerable<Type> types) =>
        RegularExpressionSites(types)
            .Where(site => string.IsNullOrWhiteSpace(site.Attribute.ErrorMessage)
                && string.IsNullOrWhiteSpace(site.Attribute.ErrorMessageResourceName))
            .Select(site => site.Site);

    private class ModelWithoutMessages
    {
        [RegularExpression("a")]
        public string Property { get; init; } = "";

        public void Action([RegularExpression("a")] string parameter)
        {
        }
    }

    private class ModelWithMessages
    {
        [RegularExpression("a", ErrorMessage = "Use an a.")]
        public string Property { get; init; } = "";

        public void Action([RegularExpression("a", ErrorMessage = "Use an a.")] string parameter)
        {
        }
    }
}
