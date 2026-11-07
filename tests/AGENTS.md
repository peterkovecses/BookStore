# Testing Standards

## 1. Naming Conventions

- **SUT naming:** Name the system under test `sut` (local variable) or `_sut` (private field).
- **Variable naming:** Use `expected` for the expected value and `actual` for the result being tested.
- **Method naming:** MUST follow the `MethodName_WhenScenario_ShouldExpectedResult` pattern (e.g., `TryDeserialize_WhenValidJsonAndTypeIsPassed_ShouldDeserialize`).
  - **Exception:** Omit the `WhenScenario` part (e.g., `MethodName_ShouldExpectedResult`) if there is no specific scenario or condition being tested.
  - Avoid filler like `WhenCalled`.

## 2. Test Structure

- **AAA Pattern:** ALWAYS follow the **Arrange, Act, Assert** pattern. Do not include any other structural comments.
- **Constants in Arrange:** ALWAYS use constants or well-named variables for magic strings, numbers, or other literal values within the **Arrange** phase.
- **Constructors:** Use **Primary Constructors** in classes for injecting dependencies.
  - **Exception:** Use a **Traditional Constructor** if member initialization (e.g., the SUT) requires referencing other fields or involves logic that cannot be expressed cleanly within a **Primary Constructor**.

## 3. Assertions

- **Assertion Library:** Use **FluentAssertions** for all assertions.

### Result Type Assertions

- When testing code that uses the `Kovecses.Result` package, ALWAYS use `Kovecses.Result.FluentAssertions` for asserting `Result` objects.
- This package MUST always be in the **same version** as the `Kovecses.Result` package used by the code being tested.

**Recommended patterns:**

- Use `.Should().HaveData(data => { ... })` for deep inspection. It automatically asserts success and performs a null-check on `Data`.
- Use `.Should().WhichData` to access and assert on the result's data directly (also performs automatic success and null-checks).
- Avoid manual `actual.IsSuccess.Should().BeTrue()` or `actual.Data.Should().NotBeNull()`; use the fluent API instead.

**Example assertion methods:**

- `.BeSuccess()`
- `.BeFailure()`
- `.HaveError()`
- `.HaveValidationProperty()`
- `.HaveData(predicate)`

**Chaining:**

- Use chainable methods like `.HaveError(code).HaveValidationProperty("FieldName").HaveCount(expected)`.

### Exception Assertions

- Assert only on the exception type, not the message.
- Example: `act.Should().Throw<ArgumentNullException>();`

## 4. Test Design

- **Test Scope:** Only test classes that contain logic. NEVER test simple properties or plain data objects.

### Data-Driven Tests

- Use `[Theory]` when the expected behavior applies to multiple different inputs.

**Note:**

- If a test requires `if-else` or conditional logic within the **Assert** phase (e.g., in a `Theory`), prefer splitting it into separate `[Fact]` methods to keep assertions explicit and focused.

## 5. Mocking

- **Mocking:** Use **NSubstitute** for creating mocks and stubs.
- Use manual fakes or stubs only in special cases where the library is insufficient.

## 6. Project Structure

- **Test projects must mirror the source project structure**, including the intermediate folders (e.g., `Aspire/`, `Hosts/`, `Modules/`, `Shared/`).
- The root directory for test projects is `tests/` instead of `src/`.
- Project and namespace naming MUST follow one of these conventions:
  - `{ProjectName}.UnitTests`
  - `{ProjectName}.IntegrationTests`

## 7. xUnit v3 Features

- Prefer `TheoryDataRow<...>` or Tuples over `object[]` for strongly typed theory data.
- Use `TheoryDataRow` metadata properties (e.g., `Skip`, `Timeout`, `DisplayName`) or fluent methods (e.g., `.WithSkip(...)`) when needed.
- Use `MatrixTheoryData<...>` to automatically generate combinations of multiple data sets (2–5 sets).
- Leverage async `MemberData` when data retrieval requires asynchronous operations.
