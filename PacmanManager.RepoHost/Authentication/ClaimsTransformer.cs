using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Authentication;

public class ClaimsTransformer(IUserService userService) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        if (identity.HasClaim(c => c.Type == AuthnConstants.AppUserIdClaimType))
        {
            return principal;
        }
        
        var subject = principal.Claims.Single(c => c.Type == AuthnConstants.SubClaimType);
        var authority = principal.Claims.Single(c => c.Type == AuthnConstants.AuthorityClaimType);
        var email = principal.Claims.Single(c => c.Type == AuthnConstants.EmailClaimType);
        var name = principal.Claims.Single(c => c.Type == AuthnConstants.DisplayNameClaimType);

        var user = await userService.GetUserByExternalIdAsync(authority.Value, subject.Value);
        if (user == null)
        {
            user = await userService.GetUserByEmailAsync(email.Value) ?? await userService.CreateUserAsync(new User
            {
                DisplayName = name.Value,
                Email = email.Value
            });
            await userService.LinkToIdentityAsync(user, authority.Value, subject.Value);
        }

        identity.AddClaim(new  Claim(AuthnConstants.AppUserIdClaimType, user.Id.ToString()));
        return principal;
    }
}