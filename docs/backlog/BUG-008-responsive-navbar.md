# [BUG-008]: Navigation Menu Layout Overflow on Mobile/Tablet (Non-responsive Header)

| Attribute | Details |
| :--- | :--- |
| **Status** | `Backlog` |
| **Component** | `BookStore.UI` |
| **Severity** | `High` |
| **Created At** | 2026-07-05 |
| **Assigned To** | @kovecsespeter |

---

## 📝 Description
On mobile and tablet viewports, the navigation header bar breaks and overflows horizontally. The brand logo, navigation links, global search box, and authentication buttons clutter together, squishing the search input and pushing the Login/Register buttons outside the visible viewport.

This is because the navbar in [MainLayout.razor](file:///Users/kovecsespeter/repos/BookStore/src/Hosts/BookStore.UI/Layout/MainLayout.razor) uses the Bootstrap `navbar-expand-lg` class but lacks the necessary collapse toggler button (hamburger menu) and collapsible container wrappers.

## 🔄 Steps to Reproduce
1. Run the application and open it in a browser.
2. Shrink the browser window width (or emulate a mobile device, e.g. iPhone or iPad) to a width below `lg` (992px).
3. Observe that the navigation elements do not collapse into a menu; instead, they stay side-by-side and overflow the header layout.

## 🎯 Expected Behavior
- On desktop viewports (992px and up), elements should lay out horizontally: `[Logo] [Books] ------- [Search Bar] ------- [Login] [Register]`.
- On viewports below 992px, navigation links, the global search box, and the authentication buttons should collapse into a vertical menu.
- A hamburger menu toggle button (`navbar-toggler`) should appear on the right side of the header. Clicking this button should slide or toggle open the collapsed menu.

## ❌ Actual Behavior
No collapsing occurs on smaller viewports, resulting in a distorted, overflowing header menu.

## 🧪 Acceptance Criteria
- [ ] Add a `navbar-toggler` button with the standard hamburger icon in `MainLayout.razor`.
- [ ] Wrap navigation links, the search box, and auth buttons in a collapsible wrapper controlled by a Blazor state property (e.g. `_collapseNavMenu`).
- [ ] Adjust margins and spacing on collapsed elements (`mx-lg-4`, `my-2 my-lg-0`, etc.) to look clean and professional when stacked vertically.
- [ ] Verify that at all resolutions from 320px up to 1920px, the header is fully functional and does not trigger horizontal overflow.
