# Transactions Ingest

A small ASP.NET Core 10 + EF Core + SQLite API, with an Angular frontend, that reconciles
transaction data from three mocked source systems (Store Uptown, Store Downtown, Online) into a
single database, tracking new, changed, revoked and finalized transactions with a full audit trail.

## Prerequisites

- .NET 10 SDK
- Node.js 20+ and npm
- Angular CLI (`npm install -g @angular/cli`)

## Running it

**Backend**

```bash
cd TransactionsIngest.Api
dotnet run
```

Migrations are applied automatically on startup and `transactions.db` is created on first run.
The API listens on http://localhost:5080 and Swagger UI is at http://localhost:5080/swagger.

**Frontend**

```bash
cd transactions-ingest-ui
npm install
ng serve
```

Open http://localhost:4200. The backend must already be running (CORS is configured for this origin).

**Tests**

```bash
dotnet test
```

Run from the repo root (or from `TransactionsIngest.Tests`).

## Trying it end to end

1. Open the Angular app, or Swagger.
2. Click "Run Import" (or call `POST /api/import`). This reads the three files in
   `TransactionsIngest.Api/MockData/` and inserts the transactions.
3. Run it again. The result should report 0 new and 0 updated, since the import is idempotent.
4. Edit a value (for example an `amount`) in one of the `MockData/*.json` files and import again.
   That transaction is updated, and `GET /api/transactions/{id}/audit` shows the field-level change.
5. Remove an entry from a mock file and import again to see it revoked, provided its
   `transactionTime` is within the last 24 hours.

## Architecture

- **Models/**: EF Core entities. `TransactionRecord` (current state), `TransactionAuditEntry`
  (change history) and `ImportBatch` (one row per import run).
- **SourceSystems/**: the `ISourceSystemClient` abstraction and a single reusable
  `JsonFileSourceSystemClient`, registered once per mock source.
- **Services/TransactionReconciliationService**: the reconciliation engine. It diffs each source's
  current snapshot against stored state and applies insert, update, revoke, reinstate and finalize
  rules.
- **Controllers/**: `POST /api/import`, `GET /api/transactions`, `GET /api/transactions/{id}/audit`.
- **transactions-ingest-ui/**: Angular frontend with a transactions table and a manual import button.
- **TransactionsIngest.Tests/**: xUnit tests using a fake source client and a manual clock.

## Design decisions and assumptions

- **The uniqueness key is (SourceSystem, TransactionId), not TransactionId alone.** The source
  systems number their own transactions independently, so IDs collide across sources. This is a
  unique database index, not only an in-code convention.
- **Reconciliation is comparison based, not event based.** Each import diffs the source's current
  snapshot against stored state. An unchanged source produces no writes, which makes re-import
  idempotent.
- **Revocation window.** A transaction missing from a snapshot within 24 hours of its
  `TransactionTime` is revoked. Absence after that point is not treated as a revocation signal,
  because the record is considered settled by then. This is a judgment call on an ambiguous part of
  the spec.
- **Finalized is immutable.** Once a transaction passes 24 hours while still present, it is marked
  Finalized and excluded from further field comparison and revocation checks.
- **Mock timestamps.** `minutesAgo` values are resolved against each mock file's last-modified
  time, not the request-time clock, so importing an unedited file twice is truly idempotent.
  Consequence: saving any edit to a mock file moves that anchor, so every row in that file gets a
  new `TransactionTime` and will show as updated on the next import, not only the row you edited.
- **A failing source does not fail the import.** An exception from one source's fetch is logged and
  that source is skipped, so the others are still processed.

## Verification status and known limitations

- The backend was started and exercised over HTTP: the first import inserted 8 transactions, a
  second import reported 0 new and 8 unchanged, and Swagger loaded.
- The 6 xUnit tests pass (insert, update, revoke, finalize, idempotency, cross-source collisions).
- The Angular app was verified with a production `ng build` only. It was not exercised in a browser
  against the live API.
- `npm install` failed on the Google Drive (G:) folder this project was developed in, so the UI
  build was verified in a copy under a local temp directory. On a normal local disk
  `npm install` and `ng serve` should work.
- Restore reports NuGet advisories for the SQLite and Pkcs transitive packages. They did not affect
  the tests and were not upgraded.
