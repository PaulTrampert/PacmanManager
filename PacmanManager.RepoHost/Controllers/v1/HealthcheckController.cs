using Microsoft.AspNetCore.Mvc;

namespace PacmanManager.RepoHost.Controllers.v1;

[ApiController]
[Route(ControllerConstants.ControllerBaseRoute)]
public class HealthcheckController
{
    public void Get() {}
}