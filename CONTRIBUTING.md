# Contributing to PrivatePacmanRepo

Thank you for your interest in contributing! To maintain code quality and consistency, please follow these guidelines.

## Testing Requirements

*   **Unit Tests**: Every new service, logic component, or modification must include corresponding unit tests in its respective `.Test` project (e.g., `PacmanManager.AurClient.Test`). Unit tests should aim for high coverage of all code changes.
*   **End-to-End (E2E) Tests**: All new or modified API endpoints must be covered by E2E tests to ensure the entire system behaves correctly from the perspective of a client.
*   **Verification**: Every Pull Request must include a successful `dotnet build` and `dotnet test` run to ensure no regressions are introduced.

## Coding Standards & Patterns

*   **Interface-Driven Development**: Always define an interface (e.g., `IAurClient`) for new services to facilitate mocking and dependency injection, following the existing pattern in the codebase.
*   **Error Handling**: Use specific exception types (e.g., `AurServerException`) rather than generic exceptions.
*   **Dependency Management**: Before adding new NuGet packages or libraries, check the `.csproj` files of existing projects to avoid unnecessary dependency bloquet.
*   **Documentation**: All public methods, classes, interfaces, and properties must be documented using appropriate documentation comments:
    *   **C#**: Use XML documentation comments (`///`).
    *   **JavaScript/TypeScript**: Use JSDoc comments.

## Architecture & Infrastructure

*   **Docker-First Development**: Any new infrastructure requirement (e.g., Redis, RabbitMQ) must be defined and configured in `compose.yaml`.
*   **Database Migrations**: All changes to the database schema must be implemented via the `PacmanManager.Migrations` project using the established migration workflow.

## Pull Request Process

*   **Branching Strategy**: *NEVER* commit directly to `main`. All proposed changes must be submitted as a Pull Request (PR) to the `main` branch on GitHub.
*   **PR Title Convention**: The PR title must start with one of the following prefixes based on the nature of the change:
    *   `PATCH`: Primarily bug fixes.
    *   `MINOR`: Non-breaking feature additions.
    *   `MAJOR`: Breaking changes.
*   **PR Description**: The description should include:
    *   A reference to any GitHub issues that this PR fixes (e.g., `Fixes #123`).
    *   Detailed explanations of the change that are not immediately obvious from the code diff.

## Repository Metadata

*   **AGENTS.md**: If a new build or test command is introduced, it must be documented in `AGENTS.md`.
