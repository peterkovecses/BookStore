# [BUG-003]: Potential Runtime Crash when Book ImageName is Empty or Whitespace

| Attribute | Details |
| :--- | :--- |
| **Status** | `Backlog` |
| **Component** | `Catalog.Domain`, `Catalog.Infrastructure` |
| **Severity** | `High` |
| **Created At** | 2026-07-05 |
| **Assigned To** | @kovecsespeter |

---

## 📝 Description
The `AzureCatalogStorageService.GetPublicUrl(imageName)` checks that the argument is not null, empty, or whitespace, throwing an `ArgumentException` if it is:
```csharp
ArgumentException.ThrowIfNullOrWhiteSpace(imageName);
```

However, `SearchBooksHandler` and `GetBookDetailsHandler` check only for `is not null` before calling this service:
```csharp
if (book.ImageName is not null)
{
    book.ImageUrl = storageService.GetPublicUrl(book.ImageName);
}
```

If a book has an empty string `""` or a whitespace-only string `"   "` stored as its `ImageName` in the database, the check `is not null` passes, and the handler calls `storageService.GetPublicUrl(ImageName)`. This throws an `ArgumentException`, causing the entire HTTP request to fail with a `500 Internal Server Error`.

## 🔄 Steps to Reproduce
1. Manually insert or seed a book entity where `ImageName` is set to an empty string `""` or `"  "`.
2. Query either the Search endpoint (`/api/catalog/books`) or the Details endpoint (`/api/catalog/books/{id}`) for that book.
3. The request will fail and return a `500 Internal Server Error` instead of handling it gracefully.

## 🎯 Expected Behavior
The API should not crash. If the image name is empty or whitespace-only, it should treat it as if no image is present (setting `ImageUrl` to `null`).

## ❌ Actual Behavior
The API crashes with an `ArgumentException` due to strict validation in the storage service.

## 🧪 Acceptance Criteria
- [ ] Modify `SearchBooksHandler` and `GetBookDetailsHandler` to check `!string.IsNullOrWhiteSpace(book.ImageName)` before attempting to resolve the public storage URL.
- [ ] Add unit tests verifying that books with empty/whitespace image names do not trigger exceptions and result in a `null` image URL.
