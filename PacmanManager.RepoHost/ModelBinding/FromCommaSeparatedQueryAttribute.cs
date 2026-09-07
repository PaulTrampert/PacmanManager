using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PacmanManager.RepoHost.ModelBinding;

/// <summary>
/// Binds a collection-valued query string member with
/// <see cref="CommaSeparatedArrayModelBinder"/>, so that it accepts <c>?ids=a,b</c> as well as the
/// <c>?ids=a&amp;ids=b</c> form ASP.NET Core handles natively.
/// </summary>
/// <remarks>
/// The binding source stays <see cref="BindingSource.Query"/> rather than becoming
/// <see cref="BindingSource.Custom"/>, which is what <see cref="ModelBinderAttribute"/> infers from
/// a binder type on its own. Keeping it explicit means the member is still read from the query
/// string only, and is still described as a query parameter by the API explorer and therefore by
/// Swagger.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class FromCommaSeparatedQueryAttribute : ModelBinderAttribute
{
    /// <summary>
    /// Creates the attribute, bound to <see cref="CommaSeparatedArrayModelBinder"/>.
    /// </summary>
    public FromCommaSeparatedQueryAttribute() : base(typeof(CommaSeparatedArrayModelBinder))
    {
        BindingSource = BindingSource.Query;
    }
}
