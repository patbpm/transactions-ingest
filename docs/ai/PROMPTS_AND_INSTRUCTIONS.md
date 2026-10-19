# AI Prompts, Instructions and Conventions

This file records how AI assistance was directed on this project.

**Tool:** Claude, used through Claude Code, as a development assistant.

**About this record:** it is a summary, not a verbatim transcript. It does not reproduce the exact
wording of individual prompts. Each entry describes the instruction given and the outcome.


## Working approach given to the AI

- Work in stages and review each stage before moving on.
- Run the application locally after each stage and use the results to decide the next change.
- Keep responsibilities separate: controllers, reconciliation service, data access and the Angular
  frontend each have one job.
- Keep business rules testable. Time comes from `TimeProvider` so the 24-hour rules can be tested
  with a fixed clock.
- Make repeated imports behave the same when the source data has not changed (idempotency).
- Mock the source systems behind an interface, so real integrations could replace them.
- Keep configuration in `appsettings.json` and keep logging and error handling in place.
- Document design decisions and known limitations in the README instead of leaving them implicit.

## Stages and the instructions behind them

| Stage | Commit | Instruction (summary) |
|---|---|---|
| 1. Setup | `683c198` | Scaffold the ASP.NET Core API and an xUnit test project. |
| 2. Domain model | `170750e` | Model the transaction, audit entry and import batch; configure EF Core with SQLite; add the initial migration and a unique index on `(SourceSystem, TransactionId)`. |
| 3. Source systems | `9fc6195` | Mock the three source systems behind `ISourceSystemClient`, reading JSON files so data can be edited to simulate corrections, delays and removals. |
| 4. Reconciliation | `17726a8` | Implement insert, field-level update, revoke, reinstate and  writing audit entries. |
| 5. API | `d15aec1` | Add `POST /api/import`, `GET /api/transactions` and an audit endpoint; wire up dependency injection, CORS, Swagger, error handling and automatic migrations. |
| 6. Frontend | `62e91a3` | Build an Angular table showing the required columns, with a manual import button. |
| 7. Tests | `0d4e8da` | Cover insert, update, revoke, finalize, idempotency and cross-source ID collisions with a fake source client and a manual clock. |
| 8. Documentation | `3d73b30` | Write the README: build and run steps, design decisions, assumptions and limitations. |

## Follow-up instructions after local testing

These came from running the application and finding problems or gaps:

- **Repeated imports reported changes .** Running imports against fresh `DbContext`
  instances showed changes on unchanged data. Stored timestamps came back from SQLite with
  `DateTimeKind.Unspecified`, so they compared differently from UTC values. The instruction was to
  normalize before comparing and to add a regression test that imports twice with separate contexts.
- **Stable mock timestamps .** Mock times anchored to the request-time clock would drift
  on every import. The instruction was to anchor them to each mock file's last-modified time.
- **Table stuck on "loading" .** In the browser, the table stayed in its loading state
  after the API had responded. The instruction was to move component state to Angular signals.
- **Audit history view .** Add a per-transaction view of its audit entries.
- **Sorting and filtering .** Add sorting by column header, plus search and status and
  source-system filters, done in the browser.
- **Documentation updates.** Update the README to match the implemented behaviour, the
  verification performed and the known limitations.

## Modifications made to AI-generated output

- Fixed the timestamp comparison so idempotency holds across database round trips, and added the
  regression test described above.
- Changed mock timestamps to resolve against file modification time.
- Replaced plain component fields with signals after the loading-state problem appeared in the
  browser.
- Treated the exercise's revocation rule as ambiguous and recorded the interpretation (see
  [DESIGN_DISCUSSION.md](DESIGN_DISCUSSION.md)).
- Corrected README text to say only what was actually run and observed.
- Reviewed the generated code, tests and documentation before committing.
