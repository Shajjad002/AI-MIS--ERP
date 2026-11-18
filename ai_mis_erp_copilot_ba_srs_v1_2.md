# AI MIS & ERP Copilot

## Business Analysis (BA) & Software Requirements Specification (SRS)

**Version:** 1.2\
**Project Type:** AI-powered Enterprise MIS / ERP Assistant\
**Architecture:** ASP.NET Core (.NET 9) + Vue 3 + SQL Server + LLM + Database-Backed Vector RAG\
**Primary Users:** Management, MIS, Finance, Operations, IT, Branch/Regional Users

**Implementation status reviewed:** 2026-11-14

## Current Implementation Status

This document defines the complete specification and architectural baseline for **AI MIS & ERP Copilot**. Version 1.2 resolves all prior prototype limitations, reporting gaps, manual database deployment risks, and multi-instance RAG scalability bottlenecks identified in v1.1.

| Sprint | Status | Current scope |
| --- | --- | --- |
| Sprint 1 — Foundation | Implemented | Clean Architecture solution, Vue 3 application, database-backed JWT authentication, automated DbUp database migrations, and user/branch access foundations. |
| Sprint 2 — AI Integration | Implemented | OpenAI-compatible LLM integration, structured interpretation, controlled schema metadata, and unified cross-database read-only views. |
| Sprint 3 — Text-to-SQL | Implemented | SQL proposal, AST/parser validation, read-only execution, and branch/region-scope enforcement. |
| Sprint 4 — Visualization | Implemented | Query UI, KPI/result summary, result table, dependency-free SVG/CSS charts, persistent conversation history, and MIS accuracy feedback loop. |
| Sprint 5 — Security | Implemented | JWT authorization, branch/region restrictions, audit logging, query timeout, rate limiting, and decoupled menu visibility roles. |
| Sprint 6 — RAG | Implemented | Authenticated document indexing, SQL Server-backed vector similarity search (supporting horizontal multi-instance scaling), and role-scoped grounded answers with citations. |
| Sprint 7 — Reporting | Implemented | Authenticated Excel/CSV workbook import, user-scoped analysis, saved reusable reports (with runtime SQL re-validation), Excel/CSV/PDF exports, and scheduled email report delivery. |

### Key Architectural & Operational Upgrades (v1.2)
* **Loan Proposal Analysis (`FR-021`):** The Vue loan proposal analysis page is fully integrated with the authenticated `/api/loan-proposals/summary` endpoint. All static prototype/sample data has been removed, and client/server 45-day scope validation is enforced.
* **Automated Database Migrations (`NFR-005`):** Manual script execution is replaced by an embedded **DbUp** migration runner (`001` through `007`) executed idempotently at application startup.
* **Multi-Database Query Abstraction (`Section 2.1`):** Cross-database joins across `ERP_MIS`, `ERP_HRM`, and `ERP_ACCOUNTING` are encapsulated in curated, read-only SQL views (e.g., `vw_AI_BranchPerformanceUnified`) to prevent LLM join hallucinations and cross-database permission errors.
* **Scalable Vector Store (`NFR-004`):** Local file-based vector indexes have been replaced with SQL Server-backed vector storage (`KnowledgeEmbedding`) with role-filtered similarity queries, supporting 1,000+ concurrent users across multiple web nodes.
* **Menu Management (`BR-006`):** Persisted menu entries, role-based menu visibility, custom menu-only roles, and per-user custom menu-role assignments remain strictly decoupled from backend JWT API authorization policies.

------------------------------------------------------------------------

# 1. Executive Summary

**AI MIS & ERP Copilot** is an AI-powered enterprise application that allows authorized users to interact with ERP/MIS data using natural language.

Instead of manually writing SQL queries, navigating multiple reports, or requesting customized reports from IT/MIS teams, users can ask questions such as:

- "Show me the total loan collection for August 2026."
- "Which branches have collection below recoverable by more than 10%?"
- "Show me the top 10 branches based on savings collection."
- "Why is Branch 0212 showing high overdue?"

The system interprets the question, identifies the relevant business entities and curated data sources, generates a safe SQL query, validates and scopes it against the user's organizational permissions, executes it against authorized read-only datasets, and presents results through tables, charts, exports, and AI-generated explanations.

------------------------------------------------------------------------

# 2. Business Analysis (BA)

## 2.1 Business Problem

Current ERP/MIS environments commonly have the following challenges:

### Data access complexity
Users need to navigate multiple screens and reports to obtain information.

### Dependency on IT/MIS
Business users frequently depend on technical personnel for customized reports.

### SQL knowledge requirement
Many useful business questions require SQL knowledge.

### Report generation time
Creating ad-hoc reports can take considerable time.

### Data interpretation
Users may receive raw numbers without an explanation of what the numbers mean.

### Multiple data sources
ERP environments span multiple logical databases/modules:
- `ERP_MIS`
- `ERP_HRM`
- `ERP_ACCOUNTING`
- `ERP_FMS`
- `ERP_INV`
- `ERP_SECURITY`

**Architectural Resolution:** Rather than exposing raw cross-database tables directly to the LLM, the database layer provides curated, indexed, read-only abstraction views inside `ERP_MIS` (such as `vw_AI_BranchPerformanceUnified`) that pre-join approved entities across modules safely and consistently.

------------------------------------------------------------------------

# 3. Proposed Business Solution

The system introduces a governed AI and validation layer between users and ERP/MIS data.

``` text
                 +---------------------+
                 |        User         |
                 +----------+----------+
                            |
                     Natural Language
                            |
                            v
                 +---------------------+
                 |   AI ERP Copilot    |
                 +----------+----------+
                            |
                 +----------v----------+
                 | Intent & Context    |
                 | Understanding       |
                 +----------+----------+
                            |
                 +----------v----------+
                 | Curated Schema &    |
                 | SQL Vector RAG      |
                 +----------+----------+
                            |
                 +----------v----------+
                 | Safe SQL Generator  |
                 +----------+----------+
                            |
                 +----------v----------+
                 | AST SQL Validation  |
                 | & Scope Injection   |
                 +----------+----------+
                            |
                            v
                  +------------------+
                  |   SQL Server     |
                  |   Read-Only DB   |
                  |  & Unified Views |
                  +--------+---------+
                           |
                           v
                +---------------------+
                | Result Processing   |
                +----------+----------+
                           |
         +-----------------+-----------------+
         |                 |                 |
         v                 v                 v
+----------------+ +---------------+ +---------------+
| AI Explanation | | Export Engine | | History & MIS |
| + Chart/Table  | | XLSX/CSV/PDF  | | Feedback Loop |
+----------------+ +---------------+ +---------------+
```

------------------------------------------------------------------------

# 4. Business Objectives

1. Reduce dependency on IT/MIS teams for routine and ad-hoc data queries.
2. Allow business users to retrieve ERP information using natural language.
3. Reduce the time required to create and distribute recurring reports.
4. Improve management decision-support capabilities with live, validated operational data.
5. Provide AI-generated explanations distinguishing calculated facts from business interpretations.
6. Improve accessibility of complex multi-module ERP information via unified read-only views.
7. Maintain strict authorization, row-level data security, and read-only database isolation.
8. Maintain a complete audit trail and continuous MIS feedback loop to improve query accuracy.

------------------------------------------------------------------------

# 5. Business Benefits

## Existing Process

``` text
User -> Request MIS Report -> MIS / IT -> Understand Requirement -> Write SQL -> Test SQL -> Generate Report -> Send to User
```

## Implemented Copilot Process

``` text
User -> Ask AI -> AI Understands Requirement -> Generate & Validate Safe SQL -> Execute with Branch Scope -> Table / Chart / Summary / Export (XLSX/PDF/CSV) -> Save or Schedule
```

Expected benefits include:
- Faster ad-hoc and scheduled reporting
- Reduced manual effort for IT/MIS teams
- Improved self-service analytics across branches and regions
- Better management visibility with traceable metrics
- Consistent business definitions governed by MIS Administrators
- Continuous accuracy improvement via user feedback triage

------------------------------------------------------------------------

# 6. Stakeholder Analysis

| Stakeholder | Responsibility |
| :--- | :--- |
| Management | View business insights, executive summaries, and scheduled reports |
| MIS Team | Validate reports, curate business terminology/metadata, and triage user feedback |
| Finance | Financial analysis and ledger reconciliation |
| Operations | Operational and loan portfolio analysis |
| Branch Users | Branch-level performance monitoring |
| IT Team | System administration and deployment operations |
| Database Administrator | Read-only database security, view optimization, and backup governance |
| Business Analyst | Requirements and business rule alignment |
| Developer | Application development and automated migration maintenance |
| System Administrator | Infrastructure, user roles, and menu visibility configuration |

------------------------------------------------------------------------

# 7. User Roles

## 7.1 System Administrator
- Manage users, system roles, and workstation/branch/region assignments
- Manage AI provider configuration and secrets
- Manage approved data sources and menu visibility catalogs
- View system and query audit logs

## 7.2 MIS Administrator
- Configure business terminology (`AIBusinessTerm`) and report definitions
- Manage approved database metadata (`AISchemaMetadata`)
- Triage user-submitted query feedback (`CopilotFeedback`) and refine schema/term definitions
- Validate AI-generated SQL patterns and review accuracy metrics

## 7.3 Management User
- Ask cross-branch/regional business questions
- View executive dashboards and live loan proposal summaries
- Save reusable reports, schedule automated email deliveries, and export reports (Excel, CSV, PDF)

## 7.4 Branch User
Can only access authorized branch data enforced at query execution time:

``` text
Branch User -> JWT Claim (BranchCode = 0212) -> Mandatory SQL Security Filter -> Only Branch 0212 Data
```

## 7.5 Regional/Area User
Can access data strictly within assigned Area/Region permissions derived from workstation assignments.

## 7.6 Menu Visibility Roles
Administrators may define custom menu-only roles and assign them to users to control which navigation entries are displayed. These assignments are strictly decoupled from system security roles and do not grant access to API endpoints, RAG documents, data, or operations protected by backend authorization policies.

------------------------------------------------------------------------

# 8. Major Use Cases

## UC-01: Ask Business Question
**Actor:** Authorized User
**Example:** *"What was the total collection during August 2026?"*
**System Process:**
1. Receive question and load conversation context.
2. Detect intent (`CollectionReport`), date range, and target entities.
3. Retrieve relevant schema metadata and business definitions.
4. Generate SQL query.
5. Parse and validate SQL (read-only, table/column whitelist).
6. Inject mandatory user branch/region access filters.
7. Execute query against read-only connection with timeout protection.
8. Persist message and metadata to conversation history.
9. Render table, chart, and AI-generated explanation with feedback controls.

## UC-02: Branch Performance Analysis
**User:** *"Show branches where collection is less than 90% of recoverable."*
**System Output:** Queries `vw_AI_BranchPerformanceUnified` (or underlying approved tables) to return Branch, Recoverable Amount, Collection Amount, Gap Amount, and Collection Percentage within the user's authorized scope.

| Branch | Recoverable | Collection | Gap | Collection % |
| :--- | ---: | ---: | ---: | ---: |
| 0212 | 12,500,000 | 9,800,000 | 2,700,000 | 78.40% |
| 0215 | 8,200,000 | 7,000,000 | 1,200,000 | 85.37% |

## UC-03: Generate Chart
**User:** *"Show August collection by branch."*
**System Output:** Identifies `Dimension = Branch`, `Measure = Collection`, `Period = August 2026`, `Chart = Bar Chart`, and renders a dependency-free SVG/CSS branch-wise bar chart.

## UC-04: Explain Report
**User:** *"Explain this report."*
**System Output:** Explains what the report measures, date range, record count, major changes, highest/lowest values, anomalies, and business interpretation—explicitly separating deterministic facts from AI interpretations.

## UC-05: Follow-up Question & Persistent History
Maintains multi-turn conversation state in `CopilotConversation` and `CopilotMessage`. Users can revisit prior sessions from the sidebar and ask contextual follow-ups (e.g., *"What about July?"*).

## UC-06: Export Report
**Supported Formats:** Excel (`.xlsx` via ClosedXML), CSV (`.csv`), and PDF (`.pdf` via QuestPDF).
**Security Flow:** Exports re-validate the underlying SQL and re-apply the requesting user's live branch/region scope before generating the file stream.

## UC-07: Saved & Scheduled AI Reports
Users can save any validated query as a reusable report (`SavedReport`) and configure automated schedules (`ScheduledReport`) using CRON expressions to generate and email Excel/PDF/CSV reports to authorized recipients.

## UC-08: Manage Menu Visibility
**Actor:** System Administrator
Administrators create/edit navigation entries, map them to implemented routes, order and group items, and assign system or custom menu-only visibility roles.

## UC-09: Submit & Triage Query Accuracy Feedback
**Actor:** End User & MIS Administrator
Users mark AI responses as `Correct` (👍) or `Incorrect` (👎) with an optional category and comment. MIS Administrators review flagged items in a dedicated triage queue to update `AIBusinessTerm` or `AISchemaMetadata`.

------------------------------------------------------------------------

# 9. Business Rules

## BR-001 — Authorization
AI must never bypass the application's existing JWT and RBAC authorization mechanisms.

## BR-002 — Branch & Region Restriction
Branch and regional users must only see data matching their authorized organizational scope, injected deterministically into the query execution pipeline.

## BR-003 — Read-Only AI Database Access
AI-generated queries are restricted exclusively to `SELECT` statements against a read-only database login. Data-modifying or schema-altering statements (`INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`, `EXEC`, `MERGE`) are unconditionally rejected.

## BR-004 — SQL Validation
Every AI-generated and saved-report SQL query must pass AST/parser validation, table/column whitelist verification, and row-level security injection before execution.

## BR-005 — Business Terminology Governance
Organization-specific terminology (`Recoverable`, `Collection`, `Outstanding`, `Overdue`, `Disbursement`, `Savings`, `Center`, `Member`, `Loan Product`, `Branch`, `EIN`) must be maintained in `AIBusinessTerm` and `AISchemaMetadata`.

## BR-006 — Menu Visibility Is Not Authorization
Menu-role grants control frontend navigation visibility only and must never be evaluated as API authorization or data-access permissions.

## BR-007 — Live Data Integrity in Operational Views
Operational analysis views (including Loan Proposal Analysis) must never display mock or placeholder data in production builds. Date ranges exceeding 45 days for non-branch users require explicit branch-level or proposed-amount scoping (`FR-021`).

------------------------------------------------------------------------

# 10. Software Requirements Specification (SRS)

## 10.1 Functional Requirements

### FR-001 Authentication
**Status: Implemented** — Secure user authentication via ASP.NET Core Identity and database-backed JWT tokens.

### FR-002 User Management
**Status: Implemented** — Administrators can create/update users, activate/deactivate accounts, and assign system roles, branches, regions, and permissions.

### FR-003 Natural Language Query
**Status: Implemented** — Users submit natural-language questions through the Copilot query interface.

### FR-004 AI Intent Detection
**Status: Implemented** — Extracts structured `Intent`, `Entity`, `Metric`, `Date`, `Filters`, `Grouping`, and `Sorting`.

### FR-005 Schema Understanding & Cross-DB Views
**Status: Implemented** — AI accesses curated JSON metadata (`AISchemaMetadata`) and unified cross-database read-only views (`vw_AI_BranchPerformanceUnified`) rather than raw system catalogs.

### FR-006 SQL Generation
**Status: Implemented** — Generates parameterized/validated T-SQL using approved schema metadata, business definitions, user scope, and conversation history.

### FR-007 SQL Validation
**Status: Implemented** — Executes a multi-stage validation pipeline: SQL Parser -> Read-Only Check -> Table/View Whitelist -> Column Validation -> Organizational Scope Injection.

### FR-008 Query Execution
**Status: Implemented** — Executes validated queries against a dedicated read-only SQL Server connection with strict command timeouts.

### FR-009 Result Visualization
**Status: Implemented** — Automatically selects and renders Data Tables, Bar Charts, Line Charts, Pie/Donut Charts, and KPI Cards.

### FR-010 AI Summary
**Status: Implemented** — Generates concise executive summaries grounded strictly in returned query metrics.

### FR-011 Conversation History
**Status: Implemented** — Persists user sessions, prompts, generated SQL, execution metrics, and chart metadata in `CopilotConversation` and `CopilotMessage`, accessible via a searchable sidebar history panel.

### FR-012 User Feedback & MIS Triage
**Status: Implemented** — Users submit `Correct` / `Incorrect` ratings with error categories and comments (`CopilotFeedback`). MIS Administrators review and resolve flagged queries in an admin triage view to refine metadata and business terms.

### FR-013 Query Audit
**Status: Implemented** — Logs User ID, question, generated SQL, execution status, duration, and timestamp to `AuditLog`.

### FR-014 Report Saving
**Status: Implemented** — Users save validated queries to `SavedReport` with optional role-based sharing. Saved reports re-run through the SQL validator and user scope filter on every execution.

### FR-015 Scheduled Reports
**Status: Implemented** — Background worker service (`IHostedService` / Hangfire) evaluates `ScheduledReport` CRON schedules, executes scoped queries, generates Excel/PDF/CSV attachments, and emails authorized internal recipients.

### FR-016 Scalable RAG Knowledge Base
**Status: Implemented** — Supports PDF, DOCX, Excel, TXT, and Markdown ingestion. Embeddings are stored in SQL Server (`KnowledgeEmbedding`) rather than local disk files to enable lock-free horizontal scaling across web nodes and role-based document filtering during similarity search.

### FR-017 Menu Catalog
**Status: Implemented** — Persists menu entries with label, sidebar section, supported destination route, icon, display order, enabled state, and visibility roles.

### FR-018 Menu Visibility and Custom Menu Roles
**Status: Implemented** — Returns enabled menu entries matching the user's system roles or assigned custom menu-only roles without granting backend API privileges.

### FR-019 Administrator Menu Management UI
**Status: Implemented** — Administrator-only screen to manage menu entries, custom menu-only roles, and per-user menu role assignments.

### FR-020 Excel/CSV Workbook Import and Analysis
**Status: Implemented** — Authenticated import of `.xlsx`, `.xls`, `.xlsm`, and `.csv` files (up to 20 MB, 20,000 rows, 100 columns), persisted per user with grouping, aggregation, summaries, and worksheet preview.

### FR-021 Loan Proposal Summary API & Live Analysis View
**Status: Implemented** — Both the authenticated `/api/loan-proposals/summary` endpoint and the Vue Loan Proposal Analysis view are fully integrated. Static sample data has been removed. Workstation-derived organizational scoping and the 45-day date-range guard (requiring branch-level or proposed-amount scope for non-branch users) are enforced end-to-end.

------------------------------------------------------------------------

# 11. Scalable RAG Architecture

To support 1,000+ concurrent users across multiple web instances without file-locking contention, vector embeddings are persisted in SQL Server:

``` text
Documents (PDF / DOCX / XLSX / MD / TXT)
    |
    v
Text Extraction & Semantic Chunking
    |
    v
Embedding Generation (Configured LLM Provider)
    |
    v
SQL Server Storage (KnowledgeDocument / KnowledgeChunk / KnowledgeEmbedding)
    |
    v
Role-Filtered Vector Similarity Search (SQL Server VECTOR_DISTANCE / Indexed Cache)
    |
    v
Grounded Context + Source Citations -> LLM -> Verified Answer
```

------------------------------------------------------------------------

# 12. Non-Functional Requirements

## NFR-001 Performance
- Simple queries and cached RAG lookups: `< 5 seconds`
- Complex analytical queries and report exports: `< 15 seconds`

## NFR-002 Security
- HTTPS/TLS enforcement
- JWT authentication & RBAC
- Database least-privilege read-only execution user
- AST SQL validation & table/column whitelisting
- Mandatory row-level branch/region filtering
- Comprehensive audit logging, query timeouts, and API rate limiting

## NFR-003 Reliability
If the external LLM provider is unavailable or times out, the system returns a graceful error message, performs zero database mutations, and logs the failure in `AuditLog`.

## NFR-004 Scalability
Stateless API and SQL Server-backed vector storage allow horizontal scaling from 100 to 1,000+ concurrent users behind Nginx or IIS without shared-disk locks.

## NFR-005 Maintainability & Automated Deployment
- Clean Architecture, SOLID principles, CQRS, Repository Pattern, and Dependency Injection.
- Automated, idempotent database schema migrations via **DbUp** (`001`–`007`) executed on application startup.

------------------------------------------------------------------------

# 13. Technology Stack

- **Frontend:** Vue 3, TypeScript, Fetch API, Dependency-free SVG/CSS visualizations
- **Backend:** .NET 9, ASP.NET Core Web API, C#, Clean Architecture, DbUp Migrations
- **Reporting & Exports:** ClosedXML (`.xlsx`), QuestPDF (`.pdf`), CsvHelper (`.csv`)
- **Database:** SQL Server (Transactional + Read-Only Replica/User + SQL Vector Storage)
- **AI & RAG:** OpenAI-compatible LLM API, Embeddings API, Structured Output, SQL-backed Vector Search
- **Infrastructure:** Docker / IIS, Nginx reverse proxy, Background Worker Service for Scheduled Reports

------------------------------------------------------------------------

# 14. Solution Architecture

``` text
AI-MIS-Copilot
|
+-- src/
|   +-- AI.MIS.Api/
|   |   +-- Controllers/ (Copilot, Reports, LoanProposals, Menus, Knowledge, Auth)
|   |   +-- Middleware/
|   |   +-- Filters/
|   |   +-- Program.cs
|   |
|   +-- AI.MIS.Application/
|   |   +-- Copilot/
|   |   +-- Reports/
|   |   +-- LoanProposals/
|   |   +-- Users/
|   |   +-- Common/
|   |
|   +-- AI.MIS.Domain/
|   |   +-- Entities/
|   |   +-- Enums/
|   |   +-- Interfaces/
|   |
|   +-- AI.MIS.Infrastructure/
|   |   +-- AI/
|   |   +-- Database/ (Migrations 001-007 via DbUp, ReadOnlyConnectionFactory)
|   |   +-- Exporting/ (ExcelExportService, PdfExportService, CsvExportService)
|   |   +-- Scheduling/ (ScheduledReportBackgroundWorker)
|   |   +-- Security/
|   |   +-- RAG/ (SqlVectorStoreService)
|   |
|   +-- AI.MIS.Persistence/
|   |
|   +-- AI.MIS.Web/
|       +-- components/ (ChatHistorySidebar, FeedbackModal, ChartRenderer)
|       +-- views/ (CopilotQueryView, LoanProposalAnalysisView, SavedReportsView, MisFeedbackTriageView, MenuAdminView)
|       +-- services/
|
+-- docs/
|   +-- AI_MIS_ERP_Copilot_BA_SRS.md
|
+-- README.md
```

------------------------------------------------------------------------

# 15. API Design

## Authentication
- `POST /api/auth/login`

## Copilot Query, History & Feedback
- `POST /api/copilot/query`
- `GET /api/copilot/conversations`
- `GET /api/copilot/conversations/{id}`
- `POST /api/copilot/feedback`
- `GET /api/copilot/feedback?reviewed=false` (MIS Administrator triage queue)
- `PUT /api/copilot/feedback/{id}/resolve` (MIS Administrator resolution)

## Loan Proposal Analysis
- `GET /api/loan-proposals/summary` (Enforces workstation scope and 45-day date guard)

## Reports, Exports & Scheduling
- `GET /api/reports`
- `POST /api/reports/save`
- `GET /api/reports/{id}`
- `POST /api/reports/{id}/export?format=xlsx|csv|pdf`
- `GET /api/reports/schedules`
- `POST /api/reports/schedules`
- `DELETE /api/reports/schedules/{id}`

## Menu Management
- `GET /api/menus`
- `GET /api/menus/manage`
- `POST /api/menus`
- `PUT /api/menus/{id}`
- `DELETE /api/menus/{id}`
- `GET /api/menus/roles`
- `POST /api/menus/roles`
- `GET /api/menus/users/{userId}/roles`
- `PUT /api/menus/users/{userId}/roles`

## Knowledge Base (RAG)
- `POST /api/knowledge/upload`
- `GET /api/knowledge`
- `DELETE /api/knowledge/{id}`

------------------------------------------------------------------------

# 16. Database Design & Automated Migrations

All schema migrations (`001` through `007`) are embedded in `AI.MIS.Infrastructure` and executed automatically at startup via **DbUp**:

- **Core & Security:** `Users`, `Roles`, `UserRoles`, `UserBranchAccess`, `UserRegionAccess`, `AuditLog`
- **Menu Management (`005`):** `MenuRoleDefinitions`, `MenuItems`, `MenuItemRoles`, `UserMenuRoles`
- **Excel Imports (`006`):** `ExcelImports`, `ExcelImportRows`
- **History, Feedback & Reporting (`007`):** `CopilotConversation`, `CopilotMessage`, `CopilotFeedback`, `SavedReport`, `ScheduledReport`
- **AI Metadata & RAG:** `AISchemaMetadata`, `AIBusinessTerm`, `AIBusinessRule`, `KnowledgeDocument`, `KnowledgeChunk`, `KnowledgeEmbedding`
- **Unified Cross-DB Read-Only Views (`007`):** `vw_AI_BranchPerformanceUnified` (joins `ERP_MIS` and `ERP_HRM` safely for AI querying)

------------------------------------------------------------------------

# 17. AI Schema Metadata & Prompt Strategy

Controlled JSON metadata (`AISchemaMetadata`) maps business concepts to approved tables and unified views, preventing the LLM from guessing cross-database join keys:

``` json
{
  "table": "vw_AI_BranchPerformanceUnified",
  "description": "Unified read-only view combining branch recoverable, loan collection, gap, collection rate, and active field officer counts.",
  "columns": [
    { "name": "BranchCode", "description": "Unique branch identifier" },
    { "name": "BranchName", "description": "Official branch name" },
    { "name": "RegionCode", "description": "Regional hierarchy code" },
    { "name": "ReportDate", "description": "Reporting date" },
    { "name": "TotalRecoverable", "description": "Total recoverable loan amount" },
    { "name": "TotalCollection", "description": "Total collected loan amount" },
    { "name": "GapAmount", "description": "Uncollected gap (Recoverable - Collection)" },
    { "name": "CollectionRatePct", "description": "Collection efficiency percentage" },
    { "name": "ActiveFieldOfficers", "description": "Count of active field officers from ERP_HRM" }
  ]
}
```

System prompt rules enforce read-only `SELECT` generation, strict adherence to approved metadata and `AIBusinessTerm` definitions, clarification prompts on ambiguous terms, and structured JSON output.

------------------------------------------------------------------------

# 18. Security Architecture

``` text
User -> LLM -> Generated SQL -> SQL AST Parser -> Read-Only Check -> Table/View Whitelist -> JWT Role & Branch/Region Filter Injection -> Query Timeout -> Read-Only SQL Login -> Audited Result
```

------------------------------------------------------------------------

# 19. MVP & Phase Completion Checklist

``` text
[✓] Login & JWT Authorization
[✓] User & Workstation Branch/Region Scoping
[✓] Natural-language MIS query interface & structured interpretation
[✓] Controlled schema metadata & unified cross-database read-only views
[✓] SQL proposal, AST validation, and read-only execution
[✓] Table results, KPI cards, and dependency-free SVG/CSS charts
[✓] Result summary & audit logging
[✓] SQL Server-backed scalable RAG answers with role filtering and citations
[✓] Administrator menu management & custom menu-only visibility roles
[✓] Persistent conversation/query history UI
[✓] User feedback collection & MIS Administrator triage workflow
[✓] Authenticated Excel/CSV workbook import and user-scoped analysis
[✓] Saved reusable reports & Excel/CSV/PDF report exports
[✓] Scheduled reports with automated email delivery
[✓] Live Loan Proposal Analysis page integrated with authenticated summary API
[✓] Automated startup database migrations via DbUp
```

------------------------------------------------------------------------

# 20. Acceptance Criteria

1. **Implemented:** An authorized user can log in via JWT authentication.
2. **Implemented:** A user can submit a natural-language MIS question and view follow-up history.
3. **Implemented:** The AI interprets queries using configured schema metadata and unified cross-database views.
4. **Implemented:** Proposed SQL is parsed and validated before read-only execution; unsafe SQL is rejected.
5. **Implemented:** System role and branch/region scope are enforced on all queries, reports, and exports.
6. **Implemented:** Results render in tables, KPI cards, and charts alongside a grounded AI summary.
7. **Implemented:** All query outcomes and errors are recorded in `AuditLog`.
8. **Implemented:** Administrators manage menu entries and custom menu-only visibility roles without altering API authorization.
9. **Implemented:** Users can save reports, schedule automated runs, and export results to Excel (`.xlsx`), CSV (`.csv`), and PDF (`.pdf`).
10. **Implemented:** Users can submit `Correct`/`Incorrect` feedback, and MIS Administrators can triage and resolve feedback items.
11. **Implemented:** Authenticated users can import Excel/CSV workbooks and view user-scoped analyses.
12. **Implemented:** The Loan Proposal Analysis page displays live data from `/api/loan-proposals/summary` with workstation scoping and 45-day validation enforced.
13. **Implemented:** Database migrations execute automatically at startup via DbUp, and RAG embeddings are stored in SQL Server for multi-instance scalability.

------------------------------------------------------------------------

# 21. Conclusion

**AI MIS & ERP Copilot (v1.2)** provides a complete, enterprise-ready AI analytics and reporting layer over existing ERP/MIS systems. By combining automated database migrations, unified cross-database read-only views, SQL Server-backed vector search, strict AST SQL validation, live operational proposal analytics, full Excel/PDF/CSV export capabilities, and an MIS feedback loop, the platform delivers self-service intelligence without compromising financial data security or governance.