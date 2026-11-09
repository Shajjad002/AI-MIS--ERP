# AI.MIS.UnitTests.Extended

Additional unit tests for **AI MIS & ERP Copilot**. This is a separate project from `tests/AI.MIS.UnitTests`
(which is left untouched), so the two can be run together or independently.

## Add it to your solution and run

From the repository root (the folder containing `AI.MIS.Copilot.sln`):

```bash
dotnet sln AI.MIS.Copilot.sln add tests/AI.MIS.UnitTests.Extended/AI.MIS.UnitTests.Extended.csproj
dotnet test tests/AI.MIS.UnitTests.Extended

# green baseline only (skip tests that describe suspected defects, see below)
dotnet test tests/AI.MIS.UnitTests.Extended --filter "Category!=SuspectedDefect"

# only the suspected-defect tests
dotnet test tests/AI.MIS.UnitTests.Extended --filter "Category=SuspectedDefect"

# with coverage (coverlet collector is referenced)
dotnet test tests/AI.MIS.UnitTests.Extended --collect:"XPlat Code Coverage"
```

No database, no network, no LLM key and no running API are needed. Tests that touch the disk use a throw-away
temp folder that is deleted afterwards. Hand-written fakes are used instead of a mocking library.

## What is covered

| Area | File | What it protects |
|---|---|---|
| AI planning | `Copilot/CopilotPlanningServiceTests` | JSON contract, blank question, non-SELECT SQL rejected, schema/prompt content |
| SQL validator | `Copilot/SqlQueryValidatorExtendedTests` | whitelist of tables/columns/aliases, writes, multi-statement, syntax errors |
| Branch authorization | `Copilot/QueryAuthorizationServiceExtendedTests` | bypass attempts: OR 1=1, IN, LIKE, NOT, UNION, JOIN, subquery, wrong alias, roles |
| Schema + audit | `Copilot/SchemaAndAuditTests` | every table has `BranchCode`; audit log format |
| Executor | `Copilot/SqlReadOnlyQueryExecutorTests` | refuses to run until the read-only connection is configured |
| Passwords / JWT | `Security/*` | PBKDF2 format, tamper cases, token claims, lifetime clamp, **token → authorizer round trip** |
| RAG service | `Rag/RagServiceTests` | chunking, 64-item embedding batches, top-K clamp, similarity filter, prompt-injection handling |
| RAG stores | `Rag/LocalStoresTests` | cosine ranking, persistence, concurrency, upload limits, path traversal, cleanup |
| Extractors | `Rag/DocumentTextExtractorTests` | TXT/MD sections, real DOCX and PDF built in memory, size limits, corrupt files |
| HTTP clients | `Rag/HttpClientsTests` | request shape, auth header, error truncation, invalid provider payloads |
| Controllers | `Api/CopilotControllerTests`, `Api/OtherControllersTests` | validate → authorize → execute order, audit trail, HTTP status mapping |
| End to end (in memory) | `CopilotPipelineTests` | real planner + validator + authorizer with a scripted LLM: unsafe SQL never reaches the executor |
| `[Authorize]` guards | `ControllerAuthorizationTests` | a deleted attribute fails a test |
| Domain | `Domain/UserSessionTests` | claim mapping, overrides, permission checks |

## Tests marked `Category=SuspectedDefect`

These assert the **correct** behaviour for problems found by reading the code. They are expected to **fail until the
code is fixed**. Each one has a comment explaining the reasoning. If one passes, the suspicion was wrong.

| Test | Suspicion |
|---|---|
| `Validate_ShouldAllowCountStar` | `COUNT(*)` has no `MultiPartIdentifier`, so `ReferenceVisitor` throws `NullReferenceException` (HTTP 500) |
| `Validate_ShouldAllowOrderByColumnAlias` | `ORDER BY Total` (a SELECT alias) is rejected as an unapproved column |
| `Validate_ShouldRejectSelectInto` | `SELECT ... INTO` is a write and is not checked |
| `Validate_ShouldRejectCrossDatabaseAndLinkedServerReferences` | only the last name part is compared with the whitelist |
| `InterpretAsync_MalformedLlmOutput_...` / `AnswerAsync_NonJson...` / `CompleteJsonAsync_Unexpected...` | `JsonException`, `KeyNotFoundException` etc. are not handled by the controllers (HTTP 500) |
| `Query_WhenTheDatabaseFails_...` | `SqlException` escapes the controller: HTTP 500 and no audit entry |
| `Query_WhenValidationRejectsTheSql_...` | rejected/unauthorized attempts are audited with `Sql = null` |
| `InterpretEndpoint_ShouldRequireAuthentication`, `SchemaMetadataEndpoint_...` | `/api/copilot/interpret` and `/api/schema-metadata` have no `[Authorize]` |
| `HavePermission_WhenGranted_...` | success message says "not authorized" |

## Honest limitations

- These tests were written without a .NET SDK, so they have **not been compiled or run**. Expect to fix a few
  small compile-level details on the first build. Assertions were checked against the source line by line.
- `Extract_CorruptDocx_IsReportedAsUnreadable` depends on which exception the OpenXml library throws for a
  non-zip stream. If it fails, add that exception type to the `catch` filter in `DocumentTextExtractor`.
- The SQL-executing code (`SqlReadOnlyQueryExecutor`, `SqlUserRepository`, `MISReportService`, menu and user
  management services) needs a real SQL Server and belongs in an integration-test project (Testcontainers is a good fit).
