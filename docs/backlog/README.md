# BookStore Backlog Board

Welcome to the central backlog board for the BookStore portfolio project. This page tracks identified bugs, active development, and features to simulate a professional engineering workflow.

## 📋 Ticket Registry

Below is the list of active bug tickets. Click on the Ticket ID to view the full details, reproduction steps, and acceptance criteria.

| Ticket ID | Title | Component | Severity | Status | Related PR / Commit |
| :--- | :--- | :--- | :--- | :--- | :--- |
| [BUG-001](BUG-001-average-rating-sorting.md) | API Validation Failure and Crash when Sorting by "Top Rated" | `Catalog.Api`, `Catalog.Domain` | `High` | `Backlog` | - |
| [BUG-002](BUG-002-price-sorting-ignores-promotion.md) | Book Price Sorting Ignores Promotion/Discounted Price | `Catalog.Domain` | `Medium` | `Backlog` | - |
| [BUG-003](BUG-003-empty-imagename-crash.md) | Potential Runtime Crash when Book ImageName is Empty or Whitespace | `Catalog.Domain`, `Catalog.Infrastructure` | `High` | `Backlog` | - |
| [BUG-004](BUG-004-soft-deleted-book-details.md) | Soft-Deleted Books are Accessible via Get Book Details Endpoint | `Catalog.Domain` | `Medium` | `Backlog` | - |
| [BUG-005](BUG-005-default-pagesize-too-large.md) | API Search Endpoint Default Page Size and Max Page Size Configuration | `Catalog.Api`, `Shared` | `Low` | `Backlog` | - |
| [BUG-006](BUG-006-filter-debounce-delay.md) | Insufficient UI Filter Debounce Delay Causing API Overload | `BookStore.UI` | `Medium` | `Backlog` | - |
| [BUG-007](BUG-007-search-box-focus-style.md) | Search Box Focus Outline Cutoff (Visual Style Glitch) | `BookStore.UI` | `Medium` | `Backlog` | - |
| [BUG-008](BUG-008-responsive-navbar.md) | Navigation Menu Layout Overflow on Mobile/Tablet (Non-responsive Header) | `BookStore.UI` | `High` | `Backlog` | - |

---

## 🛠️ Workflow Process

We follow a structured workflow to resolve tickets:
1. **Backlog**: The bug is identified, documented using the [template](template.md), and added to the registry.
2. **In Progress**: Work has started. A branch is created (e.g., `bugfix/bug-001-description`).
3. **Ready for Review**: Implementation is complete, tests are passing, and code is clean.
4. **Done**: The bugfix is merged, verified, and closed.

## 📝 Creating a New Ticket

To create a new local ticket:
1. Copy the contents of [template.md](template.md).
2. Create a new file named `BUG-[ID]-[short-description].md` (e.g., `BUG-001-price-rounding.md`) in this directory.
3. Add the ticket to the registry table above.
