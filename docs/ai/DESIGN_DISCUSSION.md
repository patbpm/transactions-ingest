# Design Discussion

The main design decisions, the reasoning behind each, and the trade-offs. 

## 1. Transaction identity is `(SourceSystem, TransactionId)`

- **Decision:** the logical key is the pair, enforced by a unique database index.
- **Why:** the brief says IDs are unique only within a source system, and the sample data uses
  `100001` in all three. `TransactionId` alone would merge unrelated transactions.
- **Trade-off:** an extra surrogate `Id` is used as the primary key, so the pair is an alternate
  key. A test covers cross-source ID collisions.

## 2. Comparison-based reconciliation

- **Decision:** each import diffs the source's current snapshot against stored state, instead of
  replaying events.
- **Why:** the source systems publish snapshots, not event streams. Diffing makes the import
  naturally idempotent, because an unchanged snapshot produces no writes, no audit entries and no
  revision bumps.
- **Trade-off:** it needs the full snapshot each time and loads the source's records into memory
  per run. That is fine for this scale and would need batching for very large sources.

## 3. Revocation window

- **Decision:** a transaction absent from a snapshot is revoked only if it is within 24 hours of
  its `TransactionTime`. Absence after that is ignored.
- **Why:** the brief says a transaction missing within 24 hours of its time is revoked, but does not
  say what absence later means. Treating old absences as revocations would risk mass revocation
  when a source simply stops returning old data.
- **Trade-off:** this is a judgment call on an ambiguous requirement. It is recorded as an
  assumption in the README.

## 4. Reinstatement

- **Decision:** a revoked transaction that reappears becomes Active again and gets a `Reinstated`
  audit entry. Any changed fields are recorded too.
- **Why:** sources can publish corrections, so revocation should not be permanent while the record
  is still mutable.

## 5. Finalization (optional requirement)

- **Decision:** a transaction still present after 24 hours becomes Finalized. Finalized records are
  not compared or changed, and are excluded from the revocation check.
- **Why:** the brief makes this optional. It gives the 24-hour boundary a clear end state and keeps
  settled history stable.
- **Trade-off:** later corrections from a source for a finalized record are ignored by design.

## 6. Audit history

- **Decision:** one audit entry per changed field (old value, new value) plus entries for created,
  revoked, reinstated and finalized events, each tied to an `ImportBatch`.
- **Why:** the brief requires field-level change records. Tying entries to a batch shows which run
  caused a change.

## 7. One DB transaction per import, isolated sources

- **Decision:** the whole import commits or rolls back together. A failure fetching one source is
  logged and that source is skipped, so the others still import.
- **Why:** no half-applied imports, while one unavailable source does not block the rest.
- **Trade-off:** a skipped source does not fail the request, so the failure is visible only in the
  logs. A production version would raise a per-source health signal.

## 8. Testability: clock and source abstractions

- **Decision:** time comes from `TimeProvider`, and source systems sit behind `ISourceSystemClient`.
- **Why:** the 24-hour rules can be tested with a manual clock and a fake source, with no waiting
  and no files.
- **Related:** the import logic sits behind `ITransactionReconciliationService`, so an external
  scheduler can call the same code the manual endpoint uses.

## 9. Mock data design

- **Decision:** one reusable JSON-file client, registered once per source. Rows use `minutesAgo`
  instead of fixed timestamps, resolved against the file's last-modified time.
- **Why:** fixed dates would age past the 24-hour window and never demonstrate revocation.
  Resolving against the request clock would shift times on every import and break idempotency.
  Editing a file is also a simple way to simulate a correction, a delayed transaction or a removal.
- **Trade-off:** saving any edit moves the anchor, so every row in that file looks updated on the
  next import. This is noted in the README.

## 10. Timestamp normalization

- **Decision:** values are normalized to UTC before comparison.
- **Why:** SQLite returns `DateTime` values as `Unspecified`, so identical instants compared as
  different and looked like changes on every re-import. A regression test imports twice using
  separate `DbContext` instances to guard against it.

## 11. Frontend scope

- **Decision:** a lightweight Angular table with the required columns, a manual import button, an
  audit-history view, and client-side sorting and filtering. State uses Angular signals.
- **Why:** the brief calls the frontend lightweight, and the optional items were low cost.
- **Trade-off:** sorting and filtering run in the browser on the full list. With large data they
  would move to the API with pagination.

## Known limitations and future work

- No pagination, and sorting and filtering are client-side.
- No guard against two imports running at the same time.
- No scheduler, as the brief specifies one already exists.
- No API authentication.
- Source clients are mocks. Real ones would need resilient HTTP handling (timeouts, retries).

