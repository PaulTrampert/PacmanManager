# AGENTS.md

## Build and Test Commands

Use these commands to verify the integrity of the codebase after making changes.

### Build
To build the entire solution:
`dotnet build`

### Test
To run all tests in the solution:
`dotnet test`

To run only the tests in a specific test project:
`dotnet test [path/to/csproj]`

To run only a specific test fixture:
`dotnet test --filter "FullyQualifiedName=Namespace.ClassName"`

To run only a specific test:
`dotnet test --filter "FullyQualifiedName=Namespace.ClassName.MethodName"`

### Lint and Typecheck
For .NET projects, checking for compilation errors via `dotnet build` serves as both a typecheck and a linting step.

## Infrastructure and Environment

The system relies on Docker Compose for its full stack. Use `docker compose up -d --build` to start the required services.

### Services
- **pacmanmanager.repohost**: The primary application service.
- **auth (Keycloak)**: Provided via Keycloak. Uses the `localdev` realm defined in `keycloak/localdev.json`.
- **migrations**: A dedicated service for running database migrations.
- **postgres**: The PostgreSQL database backend.

### Key Project Boundaries
- `LibAlpmSharp`: Core library for interacting with ALPM.
- `PacmanManager.RepoHost`: Main API and business logic implementation.
- `PacmanManager.Migrations`: Handles database schema evolution.
- `PacmanManager.AurClient`: Client for interacting with the Arch User Repository.
- `PacmanManager.CliTools`: Helper library for interacting with external command line tools.

## Version Control Standards
- Branches should start with `feature/` or `bugfix/` depending on what kind of change is being made.
- Commit early, often. If you have made a meaningful change that successfully builds, then you have probably reached a good point to commit.
- *NEVER* commit directly to `main`. Always commit to a branch.

### Opencode Specific Instructions
- Be extremely precise using the `edit` tool. The `oldString` must match the target site *exactly*.
