---
description: "C# coding standards and best practices for BreakPoint Backend"
applyTo: "**/*.cs"
---

# C# Development Instructions

Follow existing patterns in the codebase. Use modern C# features and best practices.

## Architecture

Use **Thin Controller → Service → Repository** pattern:

- **Controllers**: HTTP concerns only (routing, status codes). Delegate all logic to services.
- **Services**: Business logic and validation. Define interfaces for DI. Use DTOs for data transfer.
- **Repositories**: Data access. Extend `BaseRepository` for common operations.

## Patterns to Follow

### Controllers

- Use `[ApiController]` and `[ProducesResponseType]` attributes
- Use primary constructors for dependency injection
- Return appropriate status codes (`Ok`, `NotFound`, `CreatedAtAction`, `NoContent`)

### Services

- Define interface first (e.g., `IPlayerService`)
- Use `async`/`await` for all I/O operations
- Throw domain exceptions (e.g., `NotFoundException`) for business rule violations
- **Services must have unit tests**

### Repositories

- Extend `BaseRepository` and implement `ApplySearchFilters`
- Keep queries in repositories, not in services

### DTOs

- Use `FromModel` static methods for entity-to-DTO mapping
- Use `ToModel` methods for DTO-to-entity mapping
- Implement `IBaseDto<TEntity>` interface

## Guidelines

- Use file-scoped namespaces and primary constructors
- Use pattern matching and nullable reference types
- Register services in `ServiceCollectionExtensions`
- Follow existing naming conventions in the codebase
