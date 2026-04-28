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
- **Architectural Integrity:** This is a modular monolith. Modules must only communicate through their `Contracts` projects.
- **Modern .NET Features:** Use modern .NET 10 features like primary constructors and other latest language enhancements.
- **Expression-bodied Members:** Prefer expression-bodied methods and members where applicable.
- **Return Formatting:** If a method contains logic before the return statement, always include a blank line before the `return`.
- **Early Return:** Prefer early returns and avoid `else` or `else if` branches to reduce nesting.
- **Null Checks:** ALWAYS use `is null` instead of `== null` and `is not null` instead of `!= null`.
- **High-Performance Logging:** Avoid potentially expensive operations in logging arguments (CA1873).
    - Use `[LoggerMessage]` source generation in an `internal static partial class` (as extension methods for `ILogger`).
    - **Location:** Place the `internal static partial class` for logging in the same file as the class using it.
    - **Ambiguity Resolution:** When using Serilog alongside Microsoft Logging, always use `ILogger<T>` as the first parameter of the logging extension method to avoid `CS0104` ambiguity between `Microsoft.Extensions.Logging.ILogger` and `Serilog.ILogger`.
    - If a logging parameter requires expensive evaluation (e.g., string joining, complex formatting), wrap the call in a `logger.IsEnabled(LogLevel)` check to avoid unnecessary allocations.

## Validation Standards
- **Build Verification:** After completing a task or a significant sub-task, ALWAYS build the entire solution to verify your changes.
- **Zero Tolerance:** The build must complete with **zero errors** and **zero warnings**. If any are introduced, you must fix them before proceeding or finishing the task.
- **Command:** Use `dotnet build` or the project-specific build command to perform this verification.
