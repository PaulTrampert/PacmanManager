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
`dotnet test --filter "FullyQualifiedName=Namespace.ClassName.MethodName`

### Lint and Typecheck
For .NET projects, checking for compilation errors via `dotnet build` serves as both a typecheck and a linting step.
