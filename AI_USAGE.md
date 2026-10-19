# AI Usage Disclosure

Supporting documents:
- [Prompts, instructions and conventions](docs/ai/PROMPTS_AND_INSTRUCTIONS.md)
- [Design discussion](docs/ai/DESIGN_DISCUSSION.md)

AI tools were used as development assistants during this exercise. I used Claude via Claude Code to support implementation, explore design alternatives, troubleshoot issues, and improve test coverage and documentation.

My approach was iterative: I worked through the requirements in stages, reviewed implementation options, ran the application locally, and used the results to guide further changes. The focus was on producing a working solution with clear business rules, testable components, and documented design decisions.

## Development approach

The solution was developed in eight phases, covering project setup, domain modelling, source-system integration, reconciliation logic, API development, Angular UI implementation, testing, and documentation.

Throughout development, I focused on the separation of responsibilities between the API controllers, reconciliation service, data-access layer, and Angular frontend. I also considered how the solution could be tested reliably and how repeated imports should behave when the source data has not changed.

AI assistance was used across several technical areas:

- **ASP.NET Core and C#:** API structure, dependency injection, domain models, service implementation, and controller integration.
- **Entity Framework Core and SQLite:** database configuration, migrations, entity relationships, and uniqueness constraints.
- **Transaction reconciliation:** comparing source data with persisted records and handling creation, updates, revocation, reinstatement, and finalization.
- **Angular:** API integration, transaction display, import interaction, and audit-history functionality.
- **Testing and documentation:** xUnit test coverage, troubleshooting, regression testing, and documenting implementation decisions.

## Testing and technical refinements

Running the solution locally was an important part of the development process. Testing the API and frontend helped identify issues that were not apparent from the initial implementation.

The following refinements were made during development:

1. **Timestamp comparison and idempotency:** A repeated import could incorrectly detect changes because SQLite returned stored timestamps with `DateTimeKind.Unspecified`, while the source clients supplied UTC timestamps. The comparison logic was corrected, and a regression test was added to perform successive imports using separate `DbContext` instances.
2. **Stable mock data:** Relative mock timestamps could change between imports if calculated against the current time on each request. The implementation anchors timestamps to each mock file's last-modified time, making repeated imports deterministic until the file changes.
3. **Angular state updates:** Browser testing revealed that the transaction table could remain in its loading state after the API responded. The component was updated to use Angular signals so that state changes trigger the expected rendering behaviour.
4. **Additional functionality:** The implementation includes transaction finalization, an audit-history view, client-side sorting and filtering of the transactions table, and regression coverage for the timestamp issue.
5. **Documentation:** The README was updated to reflect the implemented behaviour, verification performed, and known limitations.

## Verification performed

- Ran `dotnet test`: all seven tests passed against an in-memory SQLite database.
- Started the API and tested the import endpoint over HTTP. The first import inserted eight transactions; the second reported zero new and eight unchanged transactions.
- Built and ran the Angular application locally against the API and verified that the transaction table loaded in the browser.
- Reviewed the implementation and documented the principal design decisions and areas for further development.

## Design considerations and future improvements

The implementation focuses on clear separation of concerns, reliable reconciliation, repeatable imports, and testable business rules.

Key decisions include using (`SourceSystem`, `TransactionId`) as the transaction identity, comparing source snapshots with persisted state, recording changes in audit history, and using a controllable time provider in tests.

For a production implementation, I would consider introducing scheduled background processing, concurrency control for overlapping imports, API authentication, server-side pagination and filtering, and resilient HTTP clients for external source systems.

This exercise provided an opportunity to apply and strengthen my knowledge of ASP.NET Core, Entity Framework Core, relational data modelling, Angular, and automated testing while using AI tools to accelerate development and investigate implementation options.
