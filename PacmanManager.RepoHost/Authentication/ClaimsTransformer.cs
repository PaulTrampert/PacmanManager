using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Authentication;

public class ClaimsTransformer(IUserService userService) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var subject = principal.Claims.Single(c => c.Type == "sub");
        var authority = principal.Claims.Single(c => c.Type == "iss");
        var email = principal.Claims.Single(c => c.Type == "email");
        var name = principal.Claims.Single(c => c.Type == "preferred_username");
        
        
    }
}