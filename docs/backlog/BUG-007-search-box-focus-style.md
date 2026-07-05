# [BUG-007]: Search Box Focus Outline Cutoff (Visual Style Glitch)

| Attribute | Details |
| :--- | :--- |
| **Status** | `Backlog` |
| **Component** | `BookStore.UI` |
| **Severity** | `Medium` |
| **Created At** | 2026-07-05 |
| **Assigned To** | @kovecsespeter |

---

## 📝 Description
When the text input in the [SearchBox](file:///Users/kovecsespeter/repos/BookStore/src/Hosts/BookStore.UI/Features/Shared/Components/SearchBox.razor) component is focused, the active blue focus border and box-shadow are applied only to the `<input>` element itself.
Because the search box is styled as an Bootstrap `input-group` (where the magnifying glass icon is in a separate `.input-group-text` container to the left), the focus outline cuts through the middle of the search box. The icon container remains gray and separate, making the control look split and visually unprofessional.

## 🔄 Steps to Reproduce
1. Navigate to `/books`.
2. Click inside the search text input to focus it.
3. Observe the blue glow. The magnifying glass icon container on the left is cut off from the glow, and a blue vertical border separates the icon from the input text field.

## 🎯 Expected Behavior
When the search input is focused, the entire input group (the icon container and the input box together) should visually behave as a single, unified control with a continuous blue focus border and box-shadow outlining the entire group.

## ❌ Actual Behavior
The focus outline splits the input group, applying only to the text input field.

## 🧪 Acceptance Criteria
- [ ] Add custom CSS rules in [app.css](file:///Users/kovecsespeter/repos/BookStore/src/Hosts/BookStore.UI/wwwroot/css/app.css) using `:focus-within` on the `.input-group` wrapper to apply unified border color and box-shadow.
- [ ] Disable the default focus shadow on the individual `.form-control` element inside the unified input group to prevent double-glows.
- [ ] Verify visually that focusing the search input highlights the entire search bar as a single element.
