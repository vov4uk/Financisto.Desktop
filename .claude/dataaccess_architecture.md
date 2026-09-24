# Financisto.DataAccess — Architecture Reference

## Purpose

The data access layer (`net10.0`; EF Core SQLite, Newtonsoft.Json, NLog). It contains an in-memory SQLite database that uses the **original Android Financisto schema**, EF Core entities, a generic repository / unit of work, and the `IFinancistoDatabase` façade that every page VM uses. It has no UI dependencies.

It is a namespace-renamed port of `Financier.DataAccess`. For Android behavior, the reference source is `C:\Code\TMP\financisto1-holo` (`DatabaseAdapter`, `backup/DatabaseImport.java`, `DatabaseExport.java`, `assets/database/`).

## Entity hierarchy

```
Entity (abstract, empty)                               Data/Entity.cs
├── IIdentity { int Id; long UpdatedOn; }              rows with a single _id key
│   ├── Account, AttributeDefinition, Budget, Category, Currency, SmsTemplate, Transaction
│   ├── CurrencyExchangeRate      ([NotMapped] Id = 1 so it survives "Id > 0" filters; real key is composite)
│   └── TagBase (abstract: Id = -1, Title, IsActive = true, UpdatedOn, SortOrder)
│        ├── Payee    : IHasAliases
│        ├── Location : IHasAliases  (+ resolved_address, lat/long, …)
│        ├── Project
│        └── Tag      (table "tag")
├── keyless / composite-key: CategoryAttribute, TransactionAttribute, CCardClosingDate, RunningBalance
└── View/TransactionsView (abstract, no [Table]) → BlotterTransactions (v_blotter),
                                                BlotterTransactionsForAccountWithSplits
```

- Money is `long` in minor units (÷100 for display). Timestamps are `long` Unix milliseconds. Amounts are never stored as float or decimal (only exchange `rate` is a `double`).
- Mapping uses the standard `System.ComponentModel.DataAnnotations.Schema` attributes `[Table]`, `[Column]`, `[ForeignKey]`, `[NotMapped]`, `[Key]`, plus this project's `Utils.IgnoreAttribute` (`[Ignore]` excludes a mapped property from backup read/write only; example: `Account.LastTransactionId`). The same attributes drive EF Core **and** the `Financisto.Adapter` backup serializer.
- Table and column name constants live in `Data/Backup.cs` (`Backup.TRANSACTION_TABLE`, `Backup.IdColumn`, …).

## Tables

| Entity | Table | Key |
|---|---|---|
| Account | `account` | `_id` |
| Transaction | `transactions` | `_id` |
| Category | `category` | `_id` |
| Currency | `currency` | `_id` |
| Budget | `budget` | `_id` |
| Payee | `payee` | `_id` |
| Location | `locations` | `_id` |
| Project | `project` | `_id` |
| Tag | `tag` | `_id` |
| AttributeDefinition | `attributes` | `_id` |
| SmsTemplate | `sms_template` | `_id` |
| CurrencyExchangeRate | `currency_exchange_rate` | (from_currency_id, to_currency_id, rate_date) |
| RunningBalance | `running_balance` | (transaction_id, account_id) |
| CategoryAttribute | `category_attribute` | keyless (`HasNoKey`) |
| TransactionAttribute | `transaction_attribute` | not `IIdentity`, never imported into the DB |
| CCardClosingDate | `ccard_closing_date` | not `IIdentity`, never imported into the DB |
| BlotterTransactions | view `v_blotter` | Id |
| BlotterTransactionsForAccountWithSplits | view `v_blotter_for_account_with_splits` | Id |

## Key Transaction fields

- `ParentId > 0` marks a split part. `ParentAccountId` is the parent's account on split parts (Android `parent_account_id`).
- `CategoryId == -1` marks a split parent. `CategoryId == 0` means no category (or a transfer).
- Transfer: `ToAccountId > 0 && CategoryId == 0`. `FromAmount < 0` is in the from-account currency and `ToAmount > 0` is in the to-account currency.
- `OriginalCurrencyId` / `OriginalFromAmount` hold a foreign-currency amount (default `0`, not null).
- `IsTemplate` is an **int** (0 = normal, 1 = template, 2 = scheduled). `Status` defaults to `"UR"`.
- `Tags` is tag titles joined by the **two literal characters `\n`** (the backup's escaped form, never unescaped in memory). `Payee.Aliases` / `Location.Aliases` use the same form; use `Utils/BackupText` (`Escape`, `Unescape`, `SplitAliases`).
- Default `Id` is `-1` on `Transaction` and `TagBase`. See the `InsertOrUpdateAsync` rule below.

## IFinancistoDatabase — main façade

```csharp
public interface IFinancistoDatabase : IUnitOfWorkFactory, IDisposable
{
    Task ImportEntitiesAsync(IEnumerable<Entity> entities);
    Task RebuildAccountBalanceAsync(int accountId);
    Task AddTransactionsAsync(IEnumerable<Transaction> transactions);
    Task<T> GetOrCreateAsync<T>(int id) where T : class, IIdentity, new();
    Task<List<T>> ExecuteQuery<T>(string query) where T : class, new();
    Task<Transaction> GetOrCreateTransactionAsync(int id);          // includes FromAccount
    Task<IEnumerable<Transaction>> GetSubTransactionsAsync(int id); // ParentId == id, includes OriginalCurrency, Category
    Task InsertOrUpdateAsync<T>(IEnumerable<T> entities) where T : Entity, IIdentity;
    Task SaveAsFile(string dest);                                   // VACUUM main INTO '<dest>'
    // IUnitOfWorkFactory: IUnitOfWork CreateUnitOfWork();
}
```

Semantics that matter:
- **`GetOrCreateAsync<T>(0)`** returns `new T { Id = 0 }`. **For a non-zero id that doesn't exist, it returns `null`.**
- **`GetOrCreateTransactionAsync(0)`** returns a new transaction dated now, with `Id = 0` and `CategoryId = 0`.
- **`InsertOrUpdateAsync`** stamps `UpdatedOn` and **inserts only when `Id == 0`**; any other id (including the default `-1`) is an `UPDATE`. Set `Id = 0` on new entities that you didn't get from `GetOrCreate*`.
- **`ExecuteQuery<T>`** runs raw SQL and maps columns to properties via `[Column]` (cached per type in `PropertyCache`). **It throws if a `[Column]` property is missing from the result set**, so the SELECT list must cover every mapped property of the target model.
- **`ImportEntitiesAsync`** does the following, in order:
  1. `SeedAsync()` builds the schema.
  2. Inserts **only `IIdentity` rows with `Id > 0`**.
  3. Runs `Backup.RESTORE_SCRIPTS` (Android's post-import fix-ups: account types, template splits, electronic account type) after the insert.
  4. Calls `RebuildAccountBalanceAsync` for every imported account.
- **`RebuildAccountBalanceAsync(accountId)`** deletes that account's `running_balance` rows and walks `v_blotter_for_account_with_splits` in date order. It skips split rows with `ParentId > 0 && IsTransfer >= 0` (only the `is_transfer = -1` half of a split transfer counts) and self-transfers. It accumulates `FromAmount`, writes `RunningBalance` rows, and updates `Account.TotalAmount`, `LastTransactionDate` and `LastTransactionId`. **Call it for every affected account** (from and to) after writing transactions.

## Repository / Unit of Work

```
IUnitOfWorkFactory.CreateUnitOfWork() → UnitOfWork<FinancistoDataContext>  (new DbContext each time; dispose it)
  ├─ GetRepository<T>() → BaseRepository<T>   (cached per type)
  │    AddAsync / Add / AddRangeAsync
  │    GetAllAsync(includes…) / LastAsync(n)
  │    FindByAsync(pred, includes…) → first or null      (AsNoTracking)
  │    FindManyAsync(pred, includes…)
  │    FindManyAndProjectAsync<TResult>(pred, projection, includes…)
  │    UpdateAsync(entity)   (Attach + State = Modified)
  │    DeleteAsync(pred, includes…) / DeleteAsync(entity)
  └─ SaveChangesAsync() / SaveChanges()     ← repositories don't save; call this
```

- `UnitOfWorkHelper.GetAllAsync<T>(this IUnitOfWork uow, includes…)` is shorthand for `uow.GetRepository<T>().GetAllAsync(...)`.
- `ExpressionExtensions.And<T>()` / `.Or<T>()` combine `Expression<Func<T,bool>>` predicates (used by the blotter filters).
- The `UnitOfWork` ctor calls `Database.EnsureCreated()`. On an unseeded DB this creates the **EF model** schema, not the Android one. Real data always goes through `ImportEntitiesAsync`, which seeds first.

## In-memory SQLite

- The `internal FinancistoDatabase()` ctor opens and keeps `SqliteConnection("Filename=:memory:")` alive for the object's lifetime. Every context shares it. Construct it via `FinancistoDatabaseFactory.CreateDatabase()`. **Each call creates a fresh, empty database.**
- `FinancistoDataContext`: `AutoDetectChangesEnabled = false`, `QueryTrackingBehavior.NoTrackingWithIdentityResolution`, composite keys for exchange rates and running balance, `HasNoKey` for `CategoryAttribute`, `ToView(...)` for the two blotter views.
- `SeedAsync()` runs embedded SQL from three `.resx` resource sets under `DataBase/Scripts/`, in this order:
  1. `SQL_create_files` (`create/*.sql`)
  2. `SQL_alter_files` (`alter/*.sql`, **ordered by resource key**, i.e. by date prefix)
  3. `SQL_views_files` (`view/*.sql`, ordered by key)

### Schema changes (keep in sync with Android)

1. Copy Android's migration into `DataBase/Scripts/alter/<yyyyMMdd_HHmm_name>.sql`.
2. Add a `ResXFileRef` `<data name="_<file name>">` entry to `SQL_alter_files.resx` and an `EmbeddedResource` line in the csproj. **A file without a resx entry never runs.** Not every file in `view/` is registered (`023[v_blotter_with_splits]` and `083[v_account]` are not).
3. Add the property to the entity with `[Column("...")]`. **The default must equal the Android DB default for NOT NULL columns** (e.g. `""`, `true`, `0`). EF inserts every mapped column explicitly, so a `null` default breaks opening older backups that lack the column.
4. If a `DbManual` query or `ExecuteQuery` model needs the column, add it to the SELECT.

## Utilities (`Utils/`)

- `IgnoreAttribute`: skip in backup serialization.
- `BackupText`: Android `aliases`/`tags` escaping (`\n` ↔ newline, `\\` ↔ `\`), `SplitAliases(escaped)`.
- `ExpressionExtensions`, `UnitOfWorkHelper`: see above.
