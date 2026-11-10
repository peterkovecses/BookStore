# 📚 BookStore

[![.NET 10](https://img.shields.io/badge/.NET-10.0-blueviolet?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![C# 14](https://img.shields.io/badge/C%23-Latest-blue?style=flat-square&logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Orchestration](https://img.shields.io/badge/.NET%20Aspire-Enabled-success?style=flat-square&logo=dotnet)](https://learn.microsoft.com/en-us/dotnet/aspire/)
[![Architecture](https://img.shields.io/badge/Architecture-Modular%20Monolith-orange?style=flat-square)](#-architecture)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)

BookStore is a .NET 10 application built to showcase modern development practices, utilizing a Modular Monolith with Vertical Slice Architecture, Minimal APIs, and .NET Aspire for cloud-native orchestration. It is designed with a strong focus on boundary isolation, testability, and clean code to deliver a clean, robust, and well-tested codebase. This project is continuously evolving, with new modules and features being actively built.

## 🌟 Tech Stack

- **Backend:** .NET 10, ASP.NET Core Minimal APIs, Entity Framework Core 10
- **Frontend:** Blazor WebAssembly
- **Orchestration:** .NET Aspire, OpenTelemetry, Azure Blob Storage (Azurite)
- **Database:** MS SQL Server
- **Testing:** xUnit v3, FluentAssertions, NSubstitute, Testcontainers

## 🏗️ Architecture

The system uses a **Modular Monolith** approach, with **Vertical Slice Architecture** applied internally within modules.

```mermaid
graph TD
    UI[BookStore.UI - Blazor WASM] -->|HTTP / Minimal APIs| Api[BookStore.ApiHost]
    
    subgraph Aspire Orchestration
        Host[BookStore.AppHost] -.->|Orchestrates| Api
        Host -.->|Orchestrates| Db[(MS SQL Server)]
        Host -.->|Orchestrates| Storage[(Azure Blob Storage)]
    end

    subgraph Modular Boundaries
        Api -->|Endpoints| Catalog[Catalog Module]
        Catalog -->|Boundary Capsule| CatalogContracts[Catalog.Contracts]
        
        subgraph Catalog Module Internal Slices
            GetDetails[GetBookDetails Feature]
            Search[SearchBooks Feature]
            Filters[GetSearchFilters Feature]
        end
    end
```

### Key Principles

- **Isolated Boundaries:** Modules communicate strictly through `Contracts`. Implementation details remain `internal` within module assemblies to prevent tight coupling.
- **Vertical Slices:** Features (e.g., endpoints, handlers, validators) are grouped together by capability rather than horizontal layers.
- **Quality Assurance:** Covered by isolated unit tests and full integration tests using containerized databases to verify real-world behavior.

## 📂 Project Structure

```text
├── .aspire/                 # Aspire orchestration assets
├── src/
│   ├── Hosts/               # API, Blazor UI, Aspire AppHost
│   ├── Modules/             # Core modules (e.g., Catalog)
│   └── Shared/              # Shared configurations and utilities
└── tests/                   # Unit and integration tests
```

## 🚀 Getting Started

To run the multi-service application locally:

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Aspire CLI **13.6.4**:
   ```bash
   dotnet tool install --global Aspire.Cli --version 13.6.4
   ```
2. Install and start [Docker Desktop](https://www.docker.com/products/docker-desktop/).
3. From the repository root, run:
   ```bash
   aspire run
   ```
4. Open the Aspire Dashboard URL shown in the console.

### Customer registration (backend only)

`POST /api/identity/registration` accepts JSON containing `email`, `password`,
`firstName`, and `lastName`. Names are required and limited to 50 characters;
the password must be at least six characters and include an uppercase letter,
a lowercase letter, a digit, and a non-alphanumeric character. Invalid input
returns the standard validation problem response. A valid request returns
`204 No Content` whether the normalized email is new or already
registered. Existing accounts are not modified by duplicate requests.

Identity owns its tables and migration history in the `identity` schema of
`BookStoreDb`. Development startup applies its migrations. In other environments,
apply them before starting the API, supplying the target connection securely:

```bash
dotnet ef database update --project src/Modules/Identity/BookStore.Identity \
  --startup-project src/Hosts/BookStore.Api --context IdentityDbContext \
  --connection "$BOOKSTORE_DB_CONNECTION"
```

Accounts created by this intermediate backend slice remain unconfirmed: email
verification, sign-in and onboarding UI are not yet available. No registration
email is sent.

## 🤝 Let's Connect

Feel free to reach out to discuss modern .NET architecture, software engineering, or potential opportunities.

- 💼 **LinkedIn:** [Peter Kovecses](https://www.linkedin.com/in/peterkovecses/)
- ⭐ **Give it a Star:** If you like what you see, please star this repository—it helps!
