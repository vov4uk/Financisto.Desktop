# Financisto.Adapter — Architecture Reference

## Purpose

`Financisto.Adapter` (`net10.0`, references DataAccess only) reads and writes Financisto Android `.backup` files: gzip-compressed, line-based text. It is the entry point for all data. When the user opens a backup, `EntityReader` parses it and `MainWindowVM` imports the entities into a fresh in-memory database. Saving goes through `BackupWriter`.

The Android reference implementation is `backup/DatabaseExport.java`, `DatabaseImport.java` and `Backup.java` in `C:\Code\TMP\financisto1-holo` (package `tw.tib.financisto`, DB v249).

## Backup file format

```
PACKAGE:tw.tib.financisto          (older backups: ru.orangesoftware.financisto)
VERSION_CODE:...
VERSION_NAME:...
DATABASE_VERSION:249
#START
$ENTITY:account
_id:1
title:Cash
...
$$
$ENTITY:transactions
...
$$
#END
```

- Header `KEY:VALUE` lines come before `#START`. Entity blocks run `$ENTITY:<table>` → zero or more `column:value` lines → `$$`. Constants are in `DataAccess/Data/Backup.cs`.
- `Line` (struct) splits on the **first** `:` only, because values may contain `:`.
- Every value must stay on one line. Android always escapes `aliases` and `tags` (`\n` → `\\n`, `\` → `\\`). Other columns get newlines replaced by a space (this is Android's default `backup_newlines` = off). The reader never unescapes, so aliases and tags stay in escaped form in memory.
- `sort_order` is exported **only for `account`**. The tables in `Backup.BACKUP_TABLES_WITH_SORT_ORDER` (account, sms_template, project, payee, budget, currency, locations, attributes, tag) are exported `order by sort_order`, and on import the order is rebuilt from the row order.
- Android import drops rows with `_id <= 0`, drops `updated_on`, `remote_key` and unknown columns, and fills missing columns with DB defaults.

## Key classes

| Class | Role |
|---|---|
| `BackupReader` (IDisposable) | Opens the gzip stream, reads the header into `BackupVersion`, yields body lines (`IAsyncEnumerable<string>`) until `#END` |
| `EntityReader : IEntityReader` | Maps lines to `Entity` instances, tracks per-table column order, numbers `sort_order` rows |
| `BackupWriter : IBackupWriter` | Writes header, entities in `ExportOrder`, footer. Writes to `<file>.tmp` first, then `File.Move(overwrite)` |
| `EntityExtensions` | `WriteBackupLines(entity, writer, columnData)` serializes one entity. `InBackupOrder(rows, type)` sorts rows of sort-ordered tables |
| `EntityInfo` / `EntityPropertyInfo` | Per-type factory (compiled `Expression.New`) + per-column compiled setter and `IPropertyConverter` |
| `DefaultConverter : IPropertyConverter` | String ↔ CLR conversion, always `InvariantCulture` |
| `Line` (readonly struct) | `Key` / `Value` of one `key:value` line |
| `BackupVersion` | Package, VersionCode, Version (VERSION_NAME), DatabaseVersion |

## Interfaces

```csharp
public interface IEntityReader
{
    Task<(IEnumerable<Entity> Entities, BackupVersion BackupVersion, Dictionary<string, List<string>> EntityColumnsOrder)>
        ParseBackupFileAsync(string fileName);
}

public interface IBackupWriter
{
    Task GenerateBackupAsync(IEnumerable<Entity> entities, string fileName,
                             BackupVersion backupVersion, Dictionary<string, List<string>> entityColumnsOrder);
}

public static string BackupWriter.GenerateFileName();   // "yyyyMMdd_HHmmss_fff.backup"
```

No DI: `MainWindow` news up `new EntityReader()` / `new BackupWriter()`. `GenerateBackupAsync` throws on a null `backupVersion` or null `entityColumnsOrder`, so it can only save after a backup was opened.

## Entity discovery (EntityReader.BuildEntityTypes)

A lazy, thread-safe, one-time scan of the `Financisto.DataAccess` assembly picks types that:
1. are assignable to `Entity`, and
2. have `[Table("name")]` (keyed by table name). Views (`TransactionsView` subclasses) have no `[Table]`, so they are skipped.

For each public property it skips `[Ignore]` and includes `[Column("name")]` (keyed by column name). **Unknown tables are dropped** (their rows are lost on save). Unknown columns of known tables are not set on the entity but are still recorded in the column order.

## Reading details

- Column order: `ParseBackupFileAsync` builds `Dictionary<string, List<string>> EntityColumnsOrder` (table → columns in first-seen order). A `HashSet` per table plus a `prevField` pointer inserts newly seen columns right after the previous column (`order.Insert(IndexOf(prevField) + 1, key)`), so rows with optional columns merge into one stable order.
- `sort_order`: for tables with a sort order, a row that has no `sort_order` line gets `SortOrder = ++rowNum` in file order, like Android's `DatabaseImport`. The counter is shared across tables; only the relative order within a table matters.

## Writing details

- **Export order** (`BackupWriter.ExportOrder`): Account → AttributeDefinition → CategoryAttribute → TransactionAttribute → Budget → Category → Currency → Location → Project → Transaction → Payee → Tag → CCardClosingDate → SmsTemplate → CurrencyExchangeRate. **Types missing from this array are never written.**
- Rows of each type go through `InBackupOrder`. Sort-ordered tables are ordered by `sort_order`, and rows created in the app (`sort_order` 0) go last.
- Per entity (`WriteBackupLines`), cached per type in `_typeCache`:
  - The table name comes from `[Table]`. Columns are all `[Column]` properties minus `[Ignore]`, and minus `sort_order` unless the table is `account`.
  - `null` values are skipped.
  - A table that **is in the source column order**: known columns go into their original slots. A column the source backup didn't have is written after them **only if its value differs from the type's default** (so untouched rows keep the source format).
  - A table **absent from the source backup** (e.g. the first `tag` created in the app) is written with all its columns.
  - Values go through `ToSingleLine`: newlines become `\n` for `aliases`/`tags` and a space elsewhere.

## DefaultConverter

| Type | Read (string → CLR) | Write (CLR → string) |
|---|---|---|
| `bool` | `"true"`/`"false"` or int (`0`/`1`) | `0` / `1` |
| `double`, `float` | invariant parse; invalid → default | `ToString("R", Invariant)`, keeps every digit (exchange rates, coordinates) |
| `IIdentity` (navigation) | — | `.Id` |
| nullable types | uses the underlying type | — |
| everything else | `Convert.ChangeType(value, type, Invariant)` | `Convert.ToString(value, Invariant)`, never culture-specific (Android can't parse e.g. U+2212 minus) |

To customize a column, implement `IPropertyConverter { Type PropertyType; object Convert(object); string ConvertBack(object); }`. Converters are hard-wired as `new DefaultConverter { PropertyType = ... }` in both `EntityReader.BuildEntityTypes` and `EntityExtensions.BuildTypeInfo`; change **both**.

## Adding a new entity type / column

1. Create the class in `Financisto.DataAccess/Data/` inheriting `Entity` (plus `IIdentity` if it has `_id`), with `[Table(Backup.XXX_TABLE)]` and `[Column(...)]` on each exported property (`[Ignore]` to skip). Give NOT NULL columns their Android DB default.
2. Add a `DbSet` to `FinancistoDataContext` (plus a key configuration if it's not `_id`), and make sure the table exists in the SQL scripts.
3. Add the type to `BackupWriter.ExportOrder` at Android's position.
4. Add it to `MainWindowVM.SaveBackup`'s collection list (Desktop). Non-`IIdentity` types are never imported into the DB, so keep them in `MainWindowVM.keyLessEntities` the same way as `CCardClosingDate`.
5. If Android exports it `order by sort_order`, add the table to `Backup.BACKUP_TABLES_WITH_SORT_ORDER`.

`EntityReader` discovers the new type automatically.

## Testing / verification

There are no test projects in this repo. To check round-trip fidelity, use a scratch console app (outside the repo, in the scratchpad) that references Adapter + DataAccess:
1. Parse a backup, import it into `FinancistoDatabaseFactory().CreateDatabase()`.
2. Replay `MainWindowVM.SaveBackup`'s collection.
3. `GenerateBackupAsync`, gunzip both files and diff them.

Prefer a synthetic `.backup` over the user's real data. The running Desktop exe locks `bin/`, so build the harness with `-o <scratch dir>`.
