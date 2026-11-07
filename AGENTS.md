# Project Rules and Conventions

## Working with the User

- **Clarification Before Implementation:** Before starting any implementation, ensure you fully understand the task, its requirements, constraints, and expected behavior. If any relevant information is missing, ambiguous, or unclear, ALWAYS ask the user for clarification. NEVER make assumptions or start implementation based on incomplete information.
- **Iterative Clarification:** Ask follow-up questions whenever necessary until you have sufficient context to proceed confidently. Do not bundle unrelated questions into unnecessarily long questionnaires; ask focused questions that help resolve specific uncertainties.
- **No Premature Implementation:** Do not modify code, create files, or make architectural decisions while critical requirements remain unclear. Wait for the user's answers before proceeding.
- **Respect User Decisions:** Follow the user's explicit requirements, preferences, and decisions. Do not silently replace them with alternative approaches. If a requirement introduces a technical concern or potential issue, explain it and discuss alternatives before proceeding.
- **Relevant Skills:** Load and follow applicable skills when they are relevant to the task, including the `git-workflow` skill for changes requiring an issue, branch, and pull request. Do not load unrelated skills.

## C# Coding Standards

- **Global Usings:** Use Global Usings consistently across the solution for commonly used namespaces. Avoid unnecessary duplication of using directives. Keep project-specific or rarely used namespaces local when appropriate.
- **Namespace Structure:** Namespaces MUST follow the project structure, starting from the project name (e.g., `BookStore.Api...`).
- **Architectural Integrity:** This is a Modular Monolith. Modules MUST communicate exclusively through their respective `Contracts` projects. Use `internal` for implementation classes within modules to maintain encapsulation.
- **Testability:** ALWAYS write testable code. Use `TimeProvider` (introduced in .NET 8) for non-deterministic time-based logic. Avoid direct calls to `DateTime.Now`. Use abstractions for time-dependent or randomly generated values (e.g., `Guid.NewGuid()`) when necessary to ensure deterministic and testable behavior.
- **Exposing Internals:** Expose internals to test projects by adding the following to the `.csproj` (or a shared `Directory.Build.props` if applicable):

  ```xml
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>$(AssemblyName).UnitTests</_Parameter1>
    </AssemblyAttribute>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>$(AssemblyName).IntegrationTests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
  ```

- **Modern .NET Features:** Prefer modern .NET 10 and C# 14 features, including primary constructors and other language enhancements, where appropriate.
- **Expression-bodied Members:** Prefer expression-bodied methods and members where applicable.
- **Return Formatting:** If a method contains logic before the return statement, ALWAYS include a blank line before the `return`.
- **Early Return:** Prefer early returns and avoid `else` or `else if` branches to reduce nesting.
- **Null Checks:** ALWAYS use `is null` instead of `== null` and `is not null` instead of `!= null`.
  - **Exception:** Within expression trees (e.g., `Expression<Func<...>>`), use `== null` and `!= null` because the `is` pattern matching operator is not supported.
- **High-Performance Logging:** Avoid potentially expensive operations in logging arguments (CA1873).
  - Use `[LoggerMessage]` source generation in an `internal static partial class`, with logging methods implemented as extension methods for `ILogger`.
  - **Location:** Place the `internal static partial class` for logging in the same file as the class using it.
  - **Ambiguity Resolution:** When using Serilog alongside Microsoft Logging, ALWAYS use `ILogger<T>` as the first parameter of the logging extension method to avoid `CS0104` ambiguity between `Microsoft.Extensions.Logging.ILogger` and `Serilog.ILogger`.
  - If a logging parameter requires expensive evaluation (e.g., string joining or complex formatting), wrap the call in a `logger.IsEnabled(LogLevel)` check to avoid unnecessary allocations.

## Testing

- **Testing Requirement:** Every code change that introduces or modifies application logic MUST include appropriate **Unit Tests** and **Integration Tests** where applicable. Exceptions include pure glue code that only connects existing components without introducing new logic. When modifying an existing feature, update its existing tests to reflect the changes and ensure that the expected behavior remains covered.
- **Test Execution Strategy:** Run only the relevant tests affected by the changes during development and before every commit. When changes affect shared components, module boundaries, cross-cutting concerns, or multiple features, run all potentially affected tests. Run the complete test suite for significant changes, especially when changes affect architecture, shared infrastructure, or multiple modules. Avoid running the entire test suite for isolated changes when targeted tests provide sufficient coverage. If no tests are relevant, such as for a documentation-only change, state this explicitly.
- **Visual Presentation:** The user verifies visual presentation manually; do not require or run visual presentation tests. This does not exempt other relevant functional tests.
- **Bug Fix Testing Requirement:** When fixing a bug, tests are ALWAYS required. A bug's existence implies either:
  1. A faulty or incorrect test (which must be corrected).
  2. An uncovered scenario or edge case (which requires adding a new test case).

  This requirement applies to both **Unit Tests** and **Integration Tests** where applicable.

## Validation Standards

- **Build Verification:** After completing a task or a significant sub-task, ALWAYS build the affected project or projects to verify your changes. Build the entire solution for significant changes, especially those involving shared components, architectural boundaries, cross-cutting concerns, or multiple projects.
- **Final Verification:** Before considering a task complete, ensure that the relevant projects build successfully. For significant changes, verify the entire solution with a full solution build.
- **Zero Tolerance:** Builds MUST complete with **zero errors** and **zero newly introduced warnings**. Fix all errors and newly introduced warnings before proceeding or finishing the task. Report any pre-existing warnings separately.
- **Command:** Use `dotnet build` or the appropriate project-specific build command to perform this verification.
