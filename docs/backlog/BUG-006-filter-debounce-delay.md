# [BUG-006]: Insufficient UI Filter Debounce Delay Causing API Overload

| Attribute | Details |
| :--- | :--- |
| **Status** | `Backlog` |
| **Component** | `BookStore.UI` |
| **Severity** | `Medium` |
| **Created At** | 2026-07-05 |
| **Assigned To** | @kovecsespeter |

---

## 📝 Description
In the book listing page, whenever a user updates a filter (e.g., ticking an Author checkbox or selecting a Tag), the change immediately triggers the list reload. 

Currently, `OnFilterChanged` uses a debounce delay of **300ms** (the default inherited from `ListComponentBase`):
```csharp
private void OnFilterChanged()
{
    Debounce(() => 
    {
        Pagination.PageNumber = 1;
        UpdateUrl();
    });
}
```

Locating and clicking multiple filter checkboxes (like selecting 3 authors or tags) takes a human user longer than 300ms. As a result, if a user checks multiple filters in sequence, the UI triggers multiple consecutive API calls to the backend, loading intermediate states and causing unnecessary database load.

## 🔄 Steps to Reproduce
1. Navigate to `/books`.
2. Check one author, wait half a second, then check another author.
3. Observe in the browser Network tab that multiple API requests are dispatched (`/api/catalog/books?...`) instead of a single request for the final selected filter state.

## 🎯 Expected Behavior
The filter change debounce for checkboxes/tags should have a longer delay (e.g. **600ms** or **700ms**). This allows the user to check multiple filters in a single action sequence, conserving API calls while maintaining a responsive, auto-applying user experience.

## ❌ Actual Behavior
Every filter checkbox click triggers an API request almost instantly due to the short 300ms delay.

## 🧪 Acceptance Criteria
- [ ] Increase the debounce delay in `OnFilterChanged` in `BookList.razor.cs` to **600ms** (or make it a separate parameter so search typing can remain at 300ms while checkboxes use 600ms).
- [ ] Verify that clicking multiple filters in quick succession (under 600ms between clicks) triggers only a single API request with the final combined filter parameters.
