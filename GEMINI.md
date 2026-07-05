# Project Rules and Conventions

## Branching, Commit, and PR Workflow (GitHub Flow)
- **Issue-Driven Development:** For any task (feature or bugfix), we must first ensure there is a corresponding GitHub Issue. During the issue's lifecycle, we actively update its status (adding comments/updates on progress if needed) until it is closed when the PR is completed/merged.
- **Branch Naming:** Start tasks by creating a branch from `master` named `bugfix/BUG-XXX-description` or `feature/BUG-XXX-description` matching the backlog ticket ID.
- **Commit Format:** Always use [Conventional Commits](https://www.conventionalcommits.org/) (e.g., `feat(catalog): ...`, `fix(catalog): ...`). Messages must be in English, concise, and single-line.
- **Local Verification:** Before pushing or opening a PR, always build the solution and run all unit/integration tests (`dotnet test`). Do not proceed if there are any failures or warnings.
- **Pushing & PR Creation:**
  1. Propose the commit message first and wait for user approval.
  2. Once approved, push the branch to GitHub (`git push origin <branch-name>`).
  3. Open a Pull Request on GitHub linking to the backlog issue/ticket (e.g., in the PR description, mention `Closes #X` where `X` is the GitHub Issue number).
- **PR Review & Approval Policy:**
  - Pull Requests require exactly **2 Approvals** before merge:
    1. One from a Review Agent (spawn a subagent to inspect code diffs and write an approval message).
    2. One from the Human User.
- **Merge & Clean Up:**
  - Always squash all commits related to the feature/bugfix branch into a single clean commit when merging the PR.
  - After the PR is merged:
    1. Delete the remote branch on GitHub.
    2. Switch back to `master` locally (`git checkout master` and `git pull`).
    3. Delete the local branch (`git branch -d <branch-name>`).


## C# Coding Standards
- **Global Usings:** Always use Global Usings for the entire solution.
- **Namespace Structure:** Namespaces must follow the project structure starting from the project name (e.g., `BookStore.Api...`).
- **Architectural Integrity:** This is a modular monolith. Modules must only communicate through their `Contracts` projects. Use `internal` for implementation classes within modules to maintain encapsulation.
- **Testability:** Always write testable code. Use `TimeProvider` (introduced in .NET 8) for non-deterministic time-based logic. Avoid direct calls to `DateTime.Now` or `Guid.NewGuid()` within logic; use abstractions where necessary.
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
- **Modern .NET Features:** Use modern .NET 10 features like primary constructors and other latest language enhancements.
- **Expression-bodied Members:** Prefer expression-bodied methods and members where applicable.
- **Return Formatting:** If a method contains logic before the return statement, always include a blank line before the `return`.
- **Early Return:** Prefer early returns and avoid `else` or `else if` branches to reduce nesting.
- **Null Checks:** ALWAYS use `is null` instead of `== null` and `is not null` instead of `!= null`.
    - **Exception:** Within expression trees (e.g., `Expression<Func<...>>`), use `== null` and `!= null` because the `is` pattern matching operator is not supported.
- **High-Performance Logging:** Avoid potentially expensive operations in logging arguments (CA1873).
    - Use `[LoggerMessage]` source generation in an `internal static partial class` (as extension methods for `ILogger`).
    - **Location:** Place the `internal static partial class` for logging in the same file as the class using it.
    - **Ambiguity Resolution:** When using Serilog alongside Microsoft Logging, always use `ILogger<T>` as the first parameter of the logging extension method to avoid `CS0104` ambiguity between `Microsoft.Extensions.Logging.ILogger` and `Serilog.ILogger`.
    - If a logging parameter requires expensive evaluation (e.g., string joining, complex formatting), wrap the call in a `logger.IsEnabled(LogLevel)` check to avoid unnecessary allocations.

## Testing Standards
- **SUT naming:** Name the system under test `sut` (local variable) or `_sut` (private field).
- **Variable naming:** Use `expected` for the expected value and `actual` for the result being tested.
- **Method naming:** Use the `MethodName_WhenScenario_ShouldExpectedResult` pattern (e.g., `TryDeserialize_WhenValidJsonAndTypeIsPassed_ShouldDeserialize`).
    - **Note:** Omit the `WhenScenario` part (e.g., `MethodName_ShouldExpectedResult`) if there is no specific scenario or condition being tested. Avoid filler like `WhenCalled`.
- **AAA Pattern:** Always follow the **Arrange**, **Act**, **Assert** pattern. Do not include any other structural comments.
- **Constants in Arrange:** ALWAYS use constants or well-named variables for magic strings, numbers, or other literal values within the **Arrange** phase.
- **Assertion Library:** Use **FluentAssertions** for all assertions.
  - **Result Type Assertions:** When testing code that uses the `Kovecses.Result` package, always use **Kovecses.Result.FluentAssertions** for asserting `Result` objects. This package must always be in the **same version** as the `Kovecses.Result` package used by the code being tested.
    - **Recommended patterns:**
        - Use `.Should().HaveData(data => { ... })` for deep inspection. It automatically asserts success and performs a null-check on `Data`.
        - Use `.Should().WhichData` to access and assert on the result's data directly (also performs automatic success and null-checks).
        - Avoid manual `actual.IsSuccess.Should().BeTrue()` or `actual.Data.Should().NotBeNull()`; use the fluent API instead.
    - **Example assertion methods:** `.BeSuccess()`, `.BeFailure()`, `.HaveError()`, `.HaveValidationProperty()`, `.HaveData(predicate)`, etc.
    - **Chaining:** Use chainable methods like `.HaveError(code).HaveValidationProperty("FieldName").HaveCount(expected)`.
- **Exception Assertions:** Assert only on the exception type, not the message (e.g., `act.Should().Throw<ArgumentNullException>();`).
- **Test Scope:** Only test classes that contain logic. Never test simple properties or plain data objects.
- **Project Structure:** Test projects must mirror the source project structure, including the intermediate folders (e.g., `Aspire/`, `Hosts/`, `Modules/`, `Shared/`). The root directory is `tests/` (instead of `src/`). Project and namespace naming should follow the `{ProjectName}.UnitTests` or `{ProjectName}.IntegrationTests` convention.
- **Constructors:** Use **Primary Constructors** in classes for injecting dependencies.
    - **Exception:** Use a **Traditional Constructor** if member initialization (e.g., the SUT) requires referencing other fields or involves logic that cannot be expressed cleanly within a **Primary Constructor**.
- **Mocking:** Use **NSubstitute** for creating mocks and stubs. Use manual fakes or stubs only in special cases where the library is insufficient.
- **Data-Driven Tests:** Use `[Theory]` when the expected behavior applies to multiple different inputs.
    - **Note:** If a test requires `if-else` or conditional logic within the **Assert** phase (e.g., in a `Theory`), prefer splitting it into separate **Fact** methods to keep assertions explicit and focused.
- **xUnit v3 Features:**
    - Prefer `TheoryDataRow<...>` or Tuples over `object[]` for strongly typed theory data.
    - Use `TheoryDataRow` metadata properties (e.g., `Skip`, `Timeout`, `DisplayName`) or fluent methods (e.g., `.WithSkip(...)`) when needed.
    - Use `MatrixTheoryData<...>` to automatically generate combinations of multiple data sets (2-5 sets).
    - Leverage async `MemberData` when data retrieval requires asynchronous operations.
- **Bug Fix Testing Requirement:** When fixing a bug, tests are ALWAYS required. A bug's existence implies either:
    1. A faulty or incorrect test (which must be corrected).
    2. An uncovered scenario or edge case (which requires adding a new test case).
    This requirement applies to both **Unit Tests** and **Integration Tests** where applicable.

## Validation Standards
- **Build Verification:** After completing a task or a significant sub-task, ALWAYS build the entire solution to verify your changes.
- **Zero Tolerance:** The build must complete with **zero errors** and **zero warnings**. If any are introduced, you must fix them before proceeding or finishing the task.
- **Command:** Use `dotnet build` or the project-specific build command to perform this verification.
