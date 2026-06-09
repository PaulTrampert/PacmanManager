# Repository Authorization Implementation Plan

## Overview

This document outlines the plan for implementing a robust authorization policy for pacman repositories. The goal is to ensure secure access while adhering to strict information-leakage prevention principles.

### Authorization Rules

| Scenario | Repository Visibility | User Relationship | Required HTTP Result |
| :--- | :--- | :--- | :--- |
| **Read** | Private | Owner | `200 OK` |
| **Read** | Private | Not Owner | `404 Not Found` (Hide existence) |
| **Read** | Public | Any | `200 OK` |
| **Write/Delete** | Private | Owner | `200/201/204 OK` |
| **Write/Delete** | Private | Not Owner | `404 Not Found` (Hide existence) |
| **Write/Delete** | Public | Not Owner | `403 Forbidden` (Identify as public, deny access) |

## Architecture Strategy: Resource-Based Authorization Filter

To maintain consistent HTTP semantics and avoid leaking whether a repository exists, we will implement a **Resource-Based Authorization Filter** using ASP.NET Core's `IAsyncAuthorizationFilter`.

### Why this approach?
1.  **Decoupling**: Controller logic remains focused on business operations, assuming authorization is handled by the middleware/filter layer.
2.  **Uniformity**: Ensures that the "404 vs 403" requirement is applied identically across all endpoints (package management, metadata, etc.).
3.  **DRY (Don't Repeat Yourself)**: Developers can protect new routes simply by decorating them with a single attribute.

---

## Implementation Roadmap

### Phase 1: Data Model Updates
Update the underlying database schema and API models to support ownership and visibility.
*   Modify `PacmanRepository` entity to include `Guid OwnerId` and `bool IsPublic`.
*   Update `Repository` API model to reflect these properties.
*   Generate Entity Framework Core migrations.

### Phase 2: Service Layer Enhancements
Enhance `IRepositoryService` to support permission-aware lookups.
*   The service must provide a way to fetch repository metadata by ID or Name efficiently.
*   Ensure the service layer acts as the "Source of Truth" for ownership and visibility status.

### Phase 3: The Authorization Filter
Implement `RepositoryAuthorizationFilter`. This filter will:
1.  Intercept the request using `RouteData` to extract the repository identifier.
2.  Resolve the `IRepositoryService` from the DI container.
3.  Fetch metadata for the target repository.
4.  Apply the logic defined in the **Authorization Rules** table above, short-circuiting the request with `NotFoundResult` or `ForbidResult` where necessary.

### Phase 4: Integration and Testing
*   Apply the new attribute to `RepositoryController`.
*   Implement comprehensive unit tests for the service layer and filter logic.
*   Perform integration testing using `WebApplicationFactory` to verify correct HTTP response codes are returned in all security edge cases.

---

## Reference Implementation (Prototypes)

### RepositoryAuthorizationFilter Prototype

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Threading.Tasks;

namespace PacmanManager.RepoHost.Attributes;

/// <summary>
/// An attribute used to trigger repository-level authorization checks.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class CheckRepositoryAccessAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _routeParameterName;

    public CheckRepositoryAccessAttribute(string routeParameterName = "id")
    {
        _routeParameterName = routeParameterName;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var routeData = context.RouteData;

        if (!routeData.Values.TryGetValue(_routeParameterName, out var value) || value == null)
        {
            context.Result = new BadRequestObjectResult($"Missing route parameter: {_routeParameterName}");
            return;
        }

        Guid repositoryId;
        if (value is Guid guidValue)
        {
            repositoryId = guidValue;
        }
        else if (Guid.TryParse(value.ToString(), out var parsedGuid))
        {
            repositoryId = parsedGuid;
        }
        else
        {
            context.Result = new BadRequestObjectResult($"Invalid format for parameter: {_routeParameterName}");
            return;
        }

        // TODO: 
        // 1. Resolve IRepositoryService from context.HttpContext.RequestServices
        // 2. Fetch repository metadata using 'repositoryId'
        // 3. Evaluate permissions (is owner? is public?)
        // 4. Set context.Result to NotFound() or Forbid() if required
    }
}
```

### Controller Usage Example

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class RepositoryController : ControllerBase
{
    [HttpGet("{id}")]
    [CheckRepositoryAccess("id")] 
    public async Task<IActionResult> GetRepository(Guid id)
    {
        // If this code is reached, the filter has already guaranteed:
        // 1. The repository exists.
        // 2. The user has permission to read it.
        return Ok(_service.GetById(id));
    }

    [HttpDelete("{id}")]
    [CheckRepositoryAccess("id")]
    public async Task<IActionResult> DeleteRepository(Guid id)
    {
        // Handled by filter: 404 if private/not-owner, 403 if public/not-owner.
        return NoContent();
    }
}
```
