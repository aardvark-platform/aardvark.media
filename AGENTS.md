# AI Agent Entry Point

This file provides AI coding assistants with essential context for working with the Aardvark.Media codebase.

For detailed documentation, see [ai/README.md](ai/README.md).

## Quick Reference

| Command | Purpose |
|---------|---------|
| `dotnet tool restore` | Restore .NET tools (Paket, Aardpack, Adaptify) |
| `dotnet paket restore` | Restore NuGet dependencies via Paket |
| `build.cmd` / `build.sh` | Build entire solution |
| `dotnet build src/Aardvark.Media.sln` | Build using .NET CLI |
| `dotnet test` | Run tests |

## Technology Stack

| Component | Version | Purpose |
|-----------|---------|---------|
| .NET | 8.0 | Runtime/SDK |
| F# | All projects (65 total) | Language |
| Paket | 9.0.2 | Dependency management |
| Aardpack | 2.0.6 | Build tooling |
| Adaptify | 1.3.5 | Code generation for adaptive models |

## Dependency Management: Paket

**DO NOT** use `dotnet add package` or edit `.fsproj` files to add dependencies.

| Task | Command |
|------|---------|
| Add dependency | `dotnet paket add <package> --project <project>` |
| Update dependency | `dotnet paket update <package>` |
| Remove dependency | `dotnet paket remove <package> --project <project>` |
| Install all | `dotnet paket restore` |

Dependencies are declared in `paket.dependencies` and `paket.references` files.

## Project Structure

```
src/
├── Aardvark.Media.sln              # Main solution
├── Aardvark.UI/                    # Core UI framework
├── Aardvark.Service/               # HTTP service layer
├── Aardvark.UI.Primitives/         # UI primitive components
├── Aardvark.Cef*/                  # CEF integration (Windows-only)
└── [62+ other F# projects]
```

## Solution Filters

| File | Platform | Purpose |
|------|----------|---------|
| `src/Aardvark.Media.sln` | All | Full solution |
| `*.slnf` files | Varies | Platform-specific filters (Windows/non-Windows) |

CEF-related projects require Windows.

## Code Generation

Adaptify generates `.g.fs` files for types marked with `[<ModelType>]`:

- Generated files: `*.g.fs`
- Source files: typically same directory as model definitions
- Trigger: build process
- **DO NOT** edit `.g.fs` files manually

## Key Namespaces

| Namespace | Purpose |
|-----------|---------|
| `Aardvark.UI` | Core UI abstractions |
| `Aardvark.Service` | HTTP/service infrastructure |
| `Aardvark.UI.Primitives` | Reusable UI components |
| `Aardvark.Cef` | Chromium Embedded Framework bindings |

## Framework Rules

1. **No C#**: All projects use F#
2. **Paket-only**: Never bypass Paket for dependency management
3. **Generated code**: Never modify `.g.fs` files
4. **Platform constraints**: CEF projects are Windows-only

## Common Failures

| Symptom | Cause | Fix |
|---------|-------|-----|
| Build fails: missing packages | Paket restore not run | `dotnet paket restore` |
| Build fails: missing tools | Tools not restored | `dotnet tool restore` |
| CEF build fails on Linux/macOS | Windows-only projects | Use solution filter excluding CEF |
| Generated code out of sync | Stale `.g.fs` | Rebuild project |

## File Ownership

| Path | Owner | Note |
|------|-------|------|
| `paket.dependencies` | Paket | Central dependency declarations |
| `paket.lock` | Paket | Locked versions (commit this) |
| `*.g.fs` | Adaptify | Generated (do not edit) |
| `.config/dotnet-tools.json` | .NET tools | Tool manifest |

## Tips for AI Agents

- Read `ai/README.md` for architecture, patterns, and detailed guidance
- Check solution filters (`.slnf`) before building on non-Windows platforms
- Run `dotnet tool restore` && `dotnet paket restore` before first build
- Respect the TDD workflow: tests first, no production code without tests
- Delete dead code rather than commenting it out
- Simplify abstractions with single implementations
- Update docs when behavior changes

## Governance

Follow prime directives in `C:\Users\sm\.claude\CLAUDE.md`:
- Zero tolerance for errors/warnings/failing tests
- TDD only: red-green-refactor
- Terse output, no fluff
- Refactor > patch, delete > comment out
