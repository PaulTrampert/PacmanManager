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

### Web front end

*   **Components render; hooks behave.** A component in `pacmanmanager-web` never calls `useEffect` itself, and holds at most a single `useState` of one small value, such as whether a panel is open. A large state object does not count as one small value. Everything else a component does (state, effects, timers, and callbacks that decide anything beyond passing a value straight through) goes in a custom hook, and the component renders what that hook returns.
    *   **A component's own behaviour** goes in a hook named after it, in the component's folder: `CopyableCodeBlock/useCopyableCodeBlock.ts`. It is not exported from the folder's `index.ts`.
    *   **Behaviour that isn't specific to one component** goes in a reusable hook of its own outside `src/components`, such as `src/useTemporaryFeedback.ts`. The component's hook uses it rather than reimplementing it.
*   **Reusable hooks return stable callbacks.** A hook outside `src/components` returns callbacks that keep the same identity for the life of the component, because its callers may list them as dependencies or pass them to memoized children. Wrap each in `useCallback` with dependencies that never change. Where a callback needs inputs that do change, keep them in a ref, updated in a `useLayoutEffect` rather than during render, which the `react-hooks` lint rules forbid, and read the ref in the callback.
    *   **A component's own `use<Component>` hook doesn't need to.** Its only caller just renders what it returns. Don't wrap its callbacks in `useCallback` unless a dependency list or a memoized child needs them stable.
*   **Test the hook directly.** Each hook gets a Vitest unit test beside it (`useCopyableCodeBlock.test.ts`, using `renderHook`) that covers its behaviour without rendering the component. The component's stories then show its states and that its interactions reach the hook.

## Architecture & Infrastructure

*   **Docker-First Development**: Any new infrastructure requirement (e.g., Redis, RabbitMQ) must be defined and configured in `compose.yaml`.
*   **Database Migrations**: All changes to the database schema must be implemented via the `PacmanManager.Migrations` project using the established migration workflow.

## Pull Request Process

*   **Branching Strategy**: *NEVER* commit directly to `main`. All proposed changes must be submitted as a Pull Request (PR) to the `main` branch on GitHub.
*   **PR Title Convention**: The PR title must start with a parenthesised change level, followed by a description -- `(<change-level>) <description>`:
    *   `(PATCH) Fix bug`: Primarily bug fixes.
    *   `(MINOR) Add new feature`: Non-breaking feature additions.
    *   `(MAJOR) Introduce breaking change`: Breaking changes.

    This is enforced by `.github/workflows/pr-title.yml`, which calls the shared
    [`check-pr-title.yml`](https://github.com/PaulTrampert/github-workflows/blob/main/.github/workflows/check-pr-title.yml),
    and it is the same form `paulhatch/semantic-version` reads when picking the next version.
*   **PR Description**: The description should include:
    *   A reference to any GitHub issues that this PR fixes (e.g., `Fixes #123`).
    *   Detailed explanations of the change that are not immediately obvious from the code diff.
    *   For any change to the UI (component changes, style changes, and so on), screenshots of the UI **before** and **after** the change. "Before" is not required if the change is purely additive. If the change is a new component that is not used anywhere yet, screenshots of its Storybook render are enough.

## Repository Metadata

*   **AGENTS.md**: If a new build or test command is introduced, it must be documented in `AGENTS.md`.
