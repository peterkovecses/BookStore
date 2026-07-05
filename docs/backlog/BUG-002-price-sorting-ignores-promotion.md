# [BUG-002]: Book Price Sorting Ignores Promotion/Discounted Price

| Attribute | Details |
| :--- | :--- |
| **Status** | `Backlog` |
| **Component** | `Catalog.Domain` |
| **Severity** | `Medium` |
| **Created At** | 2026-07-05 |
| **Assigned To** | @kovecsespeter |

---

## 📝 Description
When a user sorts the catalog by "Price" (either ascending or descending), the sorting orders items based on their base `Price` database column, ignoring any active promotional price (`Promotion.NewPrice`). 
For example, if Book A costs $50 but is discounted to $10, and Book B costs $15 (not on sale), sorting by Price (Low to High) will place Book B before Book A because the sort is using Book A's base price ($50) rather than its active sale price ($10).

## 🔄 Steps to Reproduce
1. Seed a book (Book A) with base price $50.00 and an active promotion price of $10.00.
2. Seed a book (Book B) with base price $15.00 and no promotion.
3. Query the search endpoint sorting by `Price` ascending: `/api/catalog/books?sortBy=Price&isDescending=false`.
4. Observe that Book B (price $15.00) is returned before Book A (effective price $10.00).

## 🎯 Expected Behavior
Sorting by price should order books by their *effective price* (i.e. if a promotion is active, use `Promotion.NewPrice`, otherwise use the base `Price` property).

## ❌ Actual Behavior
Sorting ordering is determined purely by the base `Price` property, ignoring discounts.

## 🧪 Acceptance Criteria
- [ ] Refactor sorting logic for the `Price` property inside `SearchBooksHandler` (or write custom sorting projection/mapping) to evaluate `Promotion != null ? Promotion.NewPrice : Price` when sorting is applied.
- [ ] Write integration tests verifying that books with promotions are placed in their mathematically correct sort order when sorting by price in both ascending and descending directions.
