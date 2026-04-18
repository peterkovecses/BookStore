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
