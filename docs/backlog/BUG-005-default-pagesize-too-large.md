# [BUG-005]: API Search Endpoint Default Page Size and Max Page Size Configuration

| Attribute | Details |
| :--- | :--- |
| **Status** | `Backlog` |
| **Component** | `Catalog.Api`, `Shared` |
| **Severity** | `Low` |
| **Created At** | 2026-07-05 |
| **Assigned To** | @kovecsespeter |

---

## 📝 Description
In `SearchBooksRequest.cs`, the default parameter value for `PageSize` is set to `IPagination.MaxPageSize` (which is `5000`):
```csharp
public record SearchBooksRequest(
    ...
    int PageSize = IPagination.MaxPageSize, // = 5000
    ...
);
```

If a client makes a search request to the books endpoint without specifying a `pageSize` query parameter (e.g. `/api/catalog/books`), the API defaults to loading, mapping, and returning up to 5000 books from the database in a single page. This poses a potential performance bottleneck, high database resource consumption, and elevated memory footprint.

Additionally, a maximum page size of `5000` is unrealistically large for a public catalog search API. In standard web services (like Stripe, Spotify, or Shopify), the maximum page size is capped at a conservative value (e.g., `100`) to protect API resources.

## 🔄 Steps to Reproduce
1. Query the search endpoint without a page size parameter: `GET /api/catalog/books`.
2. Check the raw query response or database log.
3. Observe that up to 5000 rows are selected and returned in a single page.

## 🎯 Expected Behavior
- If no page size is specified, the API should return a sensible default number of items (e.g., `IPagination.DefaultPageSize` = `15`).
- The maximum allowed page size that a client can request should be capped at `100` (by changing `IPagination.MaxPageSize` to `100`).

## ❌ Actual Behavior
The endpoint defaults to page size `5000` when omitted, and allows requesting up to `5000` items at once.

## 🧪 Acceptance Criteria
- [ ] Change the default value of `PageSize` in `SearchBooksRequest.cs` from `IPagination.MaxPageSize` to `IPagination.DefaultPageSize`.
- [ ] Change the value of `IPagination.MaxPageSize` in `IPagination.cs` from `5000` to `100`.
- [ ] Write a test verifying that when no page size is specified in the request parameters, the result uses the default page size of 15.
- [ ] Write a validation test verifying that requesting a page size greater than 100 returns a validation error.
