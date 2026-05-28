# BookStore UI Implementation Plan

## Core Technologies
- **Platform:** Blazor WebAssembly (.NET 10)
- **Reference:** `VocaBuddy` (WASM structure, feature-based organization, API client pattern)
- **Source:** `BookShelf` (UI logic, responsive design, debounced filtering)

## Requirements
- **Server-side Logic:** All filtering, sorting, and pagination must be handled server-side via the `SearchBooks` API.
- **Central Search:** Global search in the top bar redirects to the book list page with `searchTerm`.
- **UI/UX:**
    - Responsive design (Bootstrap 5, consistent spacing/typography).
    - Authentication placeholders (Login/Register buttons and fields) matching BookShelf's visual state.
    - **Debounced Filtering:** Centralized logic for delayed API calls during user input.
    - **URL State Sync:** Synchronization of filters, sorting, and pagination with the URL query string (deep-linking support).
- **Architecture:**
    - **Feature-based organization:** `Features/{FeatureName}/Pages`, `Features/{FeatureName}/Components`.
    - **Clean Dependency Management:** Reference `BookStore.Common` for `IPagination`, `ISorting`, `IFilter`. **No** reference to `ServiceDefaults`.
    - **Aspire Integration:** UI project must be orchestrated by `BookStore.AppHost`.

## Architectural Refinements (Base Classes & Components)
- **`CustomComponentBase`**:
    - Centralized `CancellationToken` management.
    - `Loading` state tracking.
    - Integration with `NavigationManager` and a UI-specific `NotificationService`.
- **`ListComponentBase<TItem, TFilter>`**:
    - Generic base for any list-based page.
    - Uses `IPagination` and `ISorting` from `Common`.
    - Built-in `Debounce` helper for search/filter inputs.
    - Automatic URL Query String mapping (mapping `searchTerm`, `page`, `sortBy` to/from URL).
- **RESTful Error Handling (ProblemDetails)**:
    - The `ApiClient` will bridge the gap between REST responses and UI logic.
    - **Success (2xx):** Map JSON body to UI-side `Result.Success<T>`.
    - **Validation Error (400):** Deserialize `ValidationProblemDetails` and map to UI-side `Result.Failure` with validation properties.
    - **Other Errors (404, 500, etc.):** Deserialize `ProblemDetails` and map to generic UI-side `Result.Failure`.
    - Components will only interact with the UI-side `Result` object, keeping them decoupled from HTTP specifics.
- **Shared Components**:
    - `Pagination`: Clean, property-driven component.
    - `RatingStars`: Stateless, visual-only component.
    - `SearchBox`: Reusable search input with built-in debounce callback.

## Implementation Checklist

### Phase 1: Project Setup & Infrastructure
- [ ] Create `BookStore.Web` (Blazor WASM) in `src/Hosts/`.
- [ ] Add project to `BookStore.sln` and `BookStore.AppHost`.
- [ ] Configure `GlobalUsings.cs` and coding standards.
- [ ] Implement `IBookStoreApiClient` with REST-to-Result mapping logic.
- [ ] Create `Common/Extensions/NavigationManagerExtensions` for QueryString parsing.

### Phase 2: Core Abstractions & Layout
- [ ] Implement `Features/Shared/BaseComponents` (`CustomComponentBase`, `ListComponentBase`).
- [ ] Implement `Features/Shared/Components` (`Pagination`, `RatingStars`, `SearchBox`).
- [ ] Implement `MainLayout` with responsive Navbar and **Central Search**.

### Phase 3: Book Feature
- [ ] Implement `Features/Books/Pages/BookList`:
    - Sidebar with `SearchBooksFilter` (Genres, Authors).
    - Grid/List view of books.
    - Integration with `SearchBooks` API.
    - Debounced filter application.
    - **Validation:** Implement client-side validation for pagination and search parameters.
- [ ] Implement `Features/Books/Pages/BookDetails`.

### Phase 4: Validation
- [ ] Full responsiveness check.
- [ ] Build verification (zero warnings/errors).
- [ ] Aspire end-to-end launch test.
