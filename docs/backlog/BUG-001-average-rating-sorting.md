# [BUG-001]: API Validation Failure and Crash when Sorting by "Top Rated"

| Attribute | Details |
| :--- | :--- |
| **Status** | `Backlog` |
| **Component** | `Catalog.Api`, `Catalog.Domain` |
| **Severity** | `High` |
| **Created At** | 2026-07-05 |
| **Assigned To** | @kovecsespeter |

---

## 📝 Description
The Blazor UI client offers a "Top Rated" sorting option in the book list dropdown, which binds the sorting parameter `SortBy` to `"AverageRating"`.
However, selecting this option fails to load the books and triggers a user-facing error message: `"Failed to load books."`

There are two root causes for this:
1. **API Validation Error:** `SearchBooksValidator` defines a list of `AllowedSortProperties` that only includes `Title`, `Price`, and `PublishedOn`. Since `AverageRating` is not allowed, the validator returns a `400 Bad Request` validation response.
2. **Dynamic Sorting Crash:** `SearchBooksHandler` applies sorting directly to the database query `IQueryable<Book>` before projection using `ApplySorting(request.Sorting)`. Since `Book` (the database entity) does not contain a property named `AverageRating` (it is a calculated property defined only on the mapped `BookSummaryDto`), the reflection-based sorting logic in `SortingExtensions.ApplySorting` will throw an `ArgumentException` at runtime (`Property 'AverageRating' not found on type 'Book'`).

## 🔄 Steps to Reproduce
1. Run the application and navigate to the Books catalogue page (`/books`).
2. Click the "Sort by" dropdown and select "Top Rated".
3. The catalog will fail to load books, displaying a generic failure notification.

## 🎯 Expected Behavior
The books should sort correctly by their average review ratings (descending or ascending) when "Top Rated" is selected, without any API errors or crashes.

## ❌ Actual Behavior
The API returns a validation error `400 Bad Request` listing `SortBy` as invalid. If validation is bypassed, the API throws `ArgumentException` and returns a `500 Internal Server Error`.

## 🧪 Acceptance Criteria
- [ ] Add `"AverageRating"` to `AllowedSortProperties` in `SearchBooksValidator`.
- [ ] Refactor the sorting logic in `SearchBooksHandler` to either apply sorting after projecting to `BookSummaryDto` (so that `AverageRating` is a valid property on the query target), or implement custom sorting translation for `AverageRating` mapping to a subquery on `Reviews.Average(r => r.Stars)`.
- [ ] Write a unit test in `SearchBooksValidatorTests` validating that `AverageRating` is accepted.
- [ ] Write integration tests verifying that sorting by `AverageRating` returns books ordered correctly in both ascending and descending directions.
