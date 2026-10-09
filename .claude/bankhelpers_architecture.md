# Bank helpers (statement import plugins) — Architecture Reference

## Purpose

Every bank statement reader is a **plugin**: one DLL in `<app>/plugins`. At startup the app scans that folder and builds the **Import** menu from what it finds, so a bank can be added, removed or replaced without rebuilding the app. The app side (loader, menu, import flow) is described in `desktop_architecture.md` ("Bank statement import"); this file covers the plugin projects and their build.

## Layout (`src/BankHelpers/`)

| Project | Role |
|---|---|
| `Financisto.BankHelpers.Abstractions` | The contract: `IBankHelper`, `ReportType`, `BankTransaction`, `BankHelperBase`, `BankParsing`. No dependencies. The app and every plugin load this one assembly. |
| `Financisto.BankHelpers.Pdf` | `BankPdfHelperBase` (Tabula + PdfPig + CsvHelper): shared by the A Bank, Pumb and Pireus plugins. **Not a plugin**: each PDF plugin embeds it. |
| `Plugins/Financisto.BankHelpers.<Bank>` | One project per bank, one DLL each: `Monobank`, `Revolut`, `Erste` (CSV), `Privat` (XLSX), `ABank` (two helpers: XLSX + PDF, they share `AbankRow`), `Pumb`, `Pireus`, `Pko` (PDF). |

`Plugins/Directory.Build.props` + `.targets` apply to every project under `Plugins/`: `net10.0`, nullable, a reference to the contract, `CopyLocalLockFileAssemblies=true`, and the embedding described below. A plugin csproj only lists its own packages (and a `ProjectReference` to `Financisto.BankHelpers.Pdf` if it reads PDFs). The Desktop csproj picks plugin projects up with the glob `..\BankHelpers\Plugins\*\*.csproj`; the solution file lists them too.

## Contract

```csharp
public interface IBankHelper
{
    string BankTitle { get; }        // menu text and account preselection; may depend on CultureInfo.CurrentUICulture
    ReportType ReportType { get; }   // Csv, Xlsx, Xls, Pdf, Json, Xml, Txt: groups the menu, filters the file dialog (GetFileExtension())
    byte[]? Icon { get; }            // encoded image bytes (PNG...), null = no icon
    IEnumerable<BankTransaction> ParseReport(string filePath);   // worker thread; throw on an unreadable file
}
```

- A plugin DLL holds one or more **public, non-abstract classes with a public parameterless constructor** implementing `IBankHelper`. Each becomes one menu entry. The instance is created once and reused for every import, so keep it stateless.
- `BankHelperBase` is the usual base class: `Icon` is the embedded resource `icon.png` of the plugin assembly (an icon from base64 is `Convert.FromBase64String(...)` in an override), and `Localized("Privat", ("uk", "Приват"))` picks the title for the UI language.
- The plugin build embeds `icon.png` from the project folder as the resource `icon.png` (60×60 PNG; `icon.svg` next to it is just the source and is not embedded).
- `BankParsing.GetDouble` / `ParseDateTime` are the shared parsing helpers (decimal comma or point, `dd.MM.yyyy H:mm[:ss]`).
- `BankTransaction`: `Date`, `Description`, `MCC`, `CardCurrencyAmount`, `OperationAmount`, `OperationCurrency` (null when it equals the card currency), `ExchangeRate`, `Commission`, `Cashback`, `Balance`. It has no CSV attributes: a CSV plugin maps its columns itself (Monobank: `MonobankMap`).
- Changing the contract breaks every plugin built against the old one, so treat it as a public API.

## One DLL per plugin

`Plugins/Directory.Build.targets` adds target `EmbedPluginDependencies` (before `AssignTargetPaths`, after `ResolveReferences`): every `.dll` in `@(ReferenceCopyLocalPaths)` (the packages and the shared project output) becomes an `EmbeddedResource` with logical name `embedded-dependencies/<assembly>.dll`. The assemblies in the `HostProvidedAssembly` list (`Financisto.BankHelpers.Abstractions.dll`, `NLog.dll`) are skipped, because the plugin must use the app's copy. `CopyLocalLockFileAssemblies=true` is what puts NuGet packages into `ReferenceCopyLocalPaths` for a class library.

This is not IL merging (ILRepack): no assembly is rewritten, so PdfPig's embedded fonts/resources and CsvHelper's reflection keep working, and a plugin's size is its own code plus the libraries (~0.2 MB for the CSV plugins, ~6 MB for the ones with PdfPig).

**The list must be kept in step with** `PluginLoadContext.HostProvidedAssemblies` in `Financisto.Desktop/Helpers/BankHelper/PluginLoadContext.cs`. A plugin that logs uses NLog (`NLog.LogManager.GetCurrentClassLogger()`): the app's NLog configuration applies.

## Loading (`Financisto.Desktop/Helpers/BankHelper/`)

- `PluginBankHelperProvider(dir)` (`IBankHelperProvider.BankHelpers`): for each `*.dll` in `dir` (top folder only, ordered by name):
  1. reads the metadata (`PEReader`) and skips the file unless it references `Financisto.BankHelpers.Abstractions`; a non-managed file or an unrelated assembly never runs any code;
  2. loads it into its own `PluginLoadContext` (an `AssemblyLoadContext`, not collectible; the plugin file stays locked while the app runs);
  3. `CreateHelpers` instantiates the helper classes; a helper whose `BankTitle` is blank, or whose constructor throws, is logged and skipped.
  Any exception for one DLL is logged (NLog) and the DLL skipped; a missing folder gives no helpers.
- `PluginLoadContext.Load(name)`: host-provided assemblies → `null` (the app's context supplies them, so `IBankHelper` is the same type on both sides); otherwise the assembly is read from the plugin's `embedded-dependencies/` resources (cached, under a lock); anything else falls back to the app (framework assemblies).
- `StartOptions.Current.PluginsPath` = `<exe dir>/plugins`, or the `FINANCISTO_PLUGINS_PATH` environment variable.
- `BankImportItem` (VM) wraps a helper for the menu: `Title` (updated when the culture changes), `ReportTypeLabel` (`CSV`), `Icon`. `MainWindowVM.ImportGroups` groups by `ReportType` in enum order and sorts by title.

## Build, publish, ship

`Financisto.Desktop.csproj` has no code reference to a plugin. It adds `ProjectReference ... ReferenceOutputAssembly="false"` for each plugin project (so they build first) and three targets:
- `GetBankHelperPluginOutputs` asks each plugin project for its `TargetPath` (`MSBuild` task with `RemoveProperties="RuntimeIdentifier;SelfContained"`; without it, `publish -r <rid>` looks for the DLL in a `<rid>` folder that a library build never creates);
- `CopyBankHelperPlugins` (after `CopyFilesToOutputDirectory`) copies the DLLs to `$(OutDir)plugins`;
- `PublishBankHelperPlugins` (before `ComputeResolvedFilesToPublishList`) adds them to the publish items as `plugins/<name>.dll` with `ExcludeFromSingleFile=true`, so they sit beside the single-file exe and are not bundled into it.

Checked: `win-x64`, `linux-x64` and `osx-arm64` publishes all contain `plugins/` (the macOS one inside `Financisto.app/Contents/MacOS`, because MacBundle copies the whole publish tree). The release zip is made from the publish folder, so it includes `plugins/`.

## Adding a bank

1. Copy a similar project under `src/BankHelpers/Plugins/` (name `Financisto.BankHelpers.<Bank>`), keep the csproj to the package references, add `icon.png`.
2. Implement a `BankHelperBase` subclass: `BankTitle`, `ReportType`, `ParseReport`.
3. Add the project to `Financisto.Desktop.slnx` (the app and the test project pick it up by glob), and a parser test next to the others in `Financisto.Desktop.Tests`.
4. Or build only the DLL and drop it into `plugins/` of an installed app: no app rebuild.

## Known limits / notes

- A plugin DLL is trusted code: loading it runs its module initializers and constructors, as in any plugin system. Only the metadata of non-plugin DLLs is read.
- Plugins can't be unloaded or replaced while the app runs (restart to pick up changes).
- The menu is built once at startup; a title that depends on the UI language is refreshed, but the order inside a group is not re-sorted.
- The localization keys `monobank`, `revolut`, `erste`, `a_bank`, `privat`, `pumb`, `pireus`, `pko`, `csv`, `xlsx`, `pdf` in `Financisto.Common` are no longer used by the app (the plugins own their titles).
