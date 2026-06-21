using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
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
        
         var subject = principal.Claims.FirstOrDefault(c => c.Type == AuthnConstants.SubClaimType);
         var authority = principal.Claims.FirstOrDefault(c => c.Type == AuthnConstants.AuthorityClaimType);
         var email = principal.Claims.FirstOrDefault(c => c.Type == AuthnConstants.EmailClaimType);
         var name = principal.Claims.FirstOrDefault(c => c.Type == AuthnConstants.DisplayNameClaimType);

         if (subject == null || authority == null || email == null)
         {
             return principal;
         }

         var user = await userService.GetUserByExternalIdAsync(authority.Value, subject.Value);
         if (user == null)
         {
             user = await userService.EnsureUserLinkedAsync(email.Value, name?.Value ?? string.Empty, authority.Value, subject.Value);
         }

         identity.AddClaim(new Claim(AuthnConstants.AppUserIdClaimType, user.Id.ToString()));
         return principal;
    }
}