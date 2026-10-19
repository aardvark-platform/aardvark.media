# AI Agent Entry Point

Start with [ai/README.md](ai/README.md) for source-linked guidance on the current 5.7.x APIs.

## Build and Test

Run from the repository root:

| Command | Purpose |
|---------|---------|
| `dotnet tool restore` | Restore Paket and Aardpack from the tool manifest |
| `dotnet paket restore` | Restore locked dependencies |
| `build.cmd` | Restore and build the full solution on Windows |
| `build.sh` | Restore and build the non-Windows solution filter |
| `dotnet build src/Aardvark.Media.sln` | Build the full solution after restore |
| `dotnet build src/Aardvark.Media.NonWindows.slnf` | Build without Windows-only projects |
| `dotnet test src/Tests/Aardvark.UI.Tests/Aardvark.UI.Tests.fsproj` | Run the test suite |

For builds without examples, use [Aardvark.Media.NoExamples.slnf](src/Aardvark.Media.NoExamples.slnf)
or [Aardvark.Media.NoExamples.NonWindows.slnf](src/Aardvark.Media.NoExamples.NonWindows.slnf).
CEF projects require Windows.

## Stack and Version Sources

All projects use F#. Consult the maintained declarations instead of duplicating versions here:

- SDK: [global.json](global.json)
- Local tools: [.config/dotnet-tools.json](.config/dotnet-tools.json)
- Dependencies, including Adaptify.Core and Adaptify.MSBuild: [paket.dependencies](paket.dependencies)
- Resolved dependency versions: [paket.lock](paket.lock)
- Target frameworks: each project's `.fsproj`

Adaptify runs through its MSBuild integration; it is not a local tool in the manifest.

## Project Structure

| Path / namespace | Purpose |
|------------------|---------|
| [src/Aardvark.UI/](src/Aardvark.UI/) / `Aardvark.UI` | Backend-independent DOM, application, HTTP, scene graph, and rendering logic |
| [src/Aardvark.UI.Giraffe/](src/Aardvark.UI.Giraffe/) / `Aardvark.UI.Giraffe` | Giraffe / ASP.NET Core backend; recommended for .NET Core applications |
| [src/Aardvark.UI.Suave/](src/Aardvark.UI.Suave/) / `Aardvark.UI.Suave` | Independent Suave backend targeting .NET Standard 2.0 |
| [src/Aardvark.UI.Primitives/](src/Aardvark.UI.Primitives/) / `Aardvark.UI.Primitives` | Controllers, dialogs, and UI components; animation and transformation APIs have their own namespaces |
| [src/Aardvark.Cef.WinForms/](src/Aardvark.Cef.WinForms/) / `Aardvark.Cef.WinForms` | CefSharp-based Windows Forms integration |
| [src/Aardvark.Cef.OffScreen/](src/Aardvark.Cef.OffScreen/) / `Aardvark.Cef.OffScreen` | CefSharp-based offscreen rendering |
| [src/Aardvark.Cef.Process/](src/Aardvark.Cef.Process/) | Custom CEF subprocess for mapped frame transfer |
| [src/Examples/](src/Examples/) | Application examples |
| [src/Tests/Aardvark.UI.Tests/](src/Tests/Aardvark.UI.Tests/) | Expecto tests, exposed through `dotnet test` |

## Dependency Management

**Paket only:** do not use `dotnet add package` or add dependencies directly to `.fsproj` files.
Dependencies belong in `paket.dependencies` and project `paket.references` files.

| Task | Command |
|------|---------|
| Add | `dotnet paket add <package> --project <project>` |
| Update | `dotnet paket update <package>` |
| Remove | `dotnet paket remove <package> --project <project>` |
| Restore | `dotnet paket restore` |

Include `paket.lock` when committing an authorized dependency change. Do not commit or push
unless explicitly instructed.

## Adaptive Models

Apply `[<ModelType>]` to the **immutable source model**. Adaptify generates its adaptive
counterpart and `.g.fs` files during builds. Do not handwrite the generated type or edit
`.g.fs` files. Rebuild the owning project if generated code is stale.
See [ARCHITECTURE.md](ai/ARCHITECTURE.md#adaptive-models).

## Engineering Rules

- F# only; respect Windows-only CEF constraints.
- Tests first for production changes; keep builds and tests free of errors and warnings.
- Delete dead code instead of commenting it out; simplify abstractions that do not earn their keep.
- Update documentation when behavior changes, checking claims against current source.
- Missing tools: run `dotnet tool restore`. Missing dependencies: run `dotnet paket restore`.
- Follow the prime directives in `C:\Users\sm\.claude\CLAUDE.md` when available.
