# [BUG-004]: Soft-Deleted Books are Accessible via Get Book Details Endpoint

| Attribute | Details |
| :--- | :--- |
| **Status** | `Backlog` |
| **Component** | `Catalog.Domain` |
| **Severity** | `Medium` |
| **Created At** | 2026-07-05 |
| **Assigned To** | @kovecsespeter |

---

## 📝 Description
Soft delete functionality is implemented on the `Book` entity via the `SoftDeleted` boolean property. In `SearchBooksFilter.cs`, soft-deleted books are correctly filtered out of search results:
```csharp
predicate = predicate.And(b => !b.SoftDeleted);
```

However, `GetBookDetailsHandler` does not check this property when retrieving a book by ID:
```csharp
var book = await dbContext.Books
    .AsNoTracking()
    .Where(book => book.Id == request.Id)
    .Select(BookMappings.ToBookDetailsDto)
    .FirstOrDefaultAsync(cancellationToken);
```
Since no global query filter is set up in EF Core, the database still returns the book even if `SoftDeleted` is `true`. This means anyone who knows the ID of a soft-deleted book can still view its full details via `/api/catalog/books/{id}`.

## 🔄 Steps to Reproduce
1. Seed a book and set its `SoftDeleted` property to `true`.
2. Send a request to get the book's details: `GET /api/catalog/books/{id}`.
3. Observe that the endpoint returns `200 OK` with the full book details instead of `404 NotFound`.

## 🎯 Expected Behavior
The details endpoint should return `404 NotFound` (or a `Result.Failure` containing a NotFound error) if the book is soft-deleted, preventing public access.

## ❌ Actual Behavior
The endpoint returns `200 OK` and exposes the soft-deleted book.

## 🧪 Acceptance Criteria
- [ ] Update `GetBookDetailsHandler` to filter out soft-deleted books (e.g. by adding `.Where(book => !book.SoftDeleted)` to the query).
- [ ] Write an integration test `GetDetails_WhenBookIsSoftDeleted_ShouldReturnNotFound` to verify this behavior.
