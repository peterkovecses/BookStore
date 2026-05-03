# Project Rules and Conventions

## Commit Standards
- **Format:** Always use [Conventional Commits](https://www.conventionalcommits.org/) (e.g., `feat:`, `fix:`, `chore:`, `docs:`).
- **Language:** Commit messages must be in English.
- **Style:** Use concise, single-line messages.
- **Approval Workflow:**
  1. Propose the commit message first and wait for user approval.
  2. After the message is approved, ask for a single final confirmation to perform both `git commit` and `git push` together.
  3. Never commit or push without explicit user consent for each operation.

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
- **AAA Pattern:** Always follow the **Arrange**, **Act**, **Assert** pattern. Do not include any other structural comments.
- **Assertion Library:** Use **FluentAssertions** for all assertions.
- **Exception Assertions:** Assert only on the exception type, not the message (e.g., `act.Should().Throw<ArgumentNullException>();`).
- **Test Scope:** Only test classes that contain logic. Never test simple properties or plain data objects.
- **Project Structure:** Test projects must mirror the source project structure. The root directory is `tests/` (instead of `src/`). Project and namespace naming should follow the `{ProjectName}.UnitTests` or `{ProjectName}.IntegrationTests` convention.
- **Constructors:** Use **Primary Constructors** in test classes for injecting shared dependencies or mocks.
- **Mocking:** Use **NSubstitute** for creating mocks and stubs. Use manual fakes or stubs only in special cases where the library is insufficient.
- **Data-Driven Tests:** Use `[Theory]` when the expected behavior applies to multiple different inputs.
- **xUnit v3 Features:**
    - Prefer `TheoryDataRow<...>` or Tuples over `object[]` for strongly typed theory data.
    - Use `TheoryDataRow` metadata properties (e.g., `Skip`, `Timeout`, `DisplayName`) or fluent methods (e.g., `.WithSkip(...)`) when needed.
    - Use `MatrixTheoryData<...>` to automatically generate combinations of multiple data sets (2-5 sets).
    - Leverage async `MemberData` when data retrieval requires asynchronous operations.

## Validation Standards
- **Build Verification:** After completing a task or a significant sub-task, ALWAYS build the entire solution to verify your changes.
- **Zero Tolerance:** The build must complete with **zero errors** and **zero warnings**. If any are introduced, you must fix them before proceeding or finishing the task.
- **Command:** Use `dotnet build` or the project-specific build command to perform this verification.
