# AI MIS & ERP Copilot

## Business Analysis (BA) & Software Requirements Specification (SRS)

**Version:** 2.0 (Microservices Architecture Edition)\
**Project Type:** AI-powered Enterprise MIS / ERP Assistant\
**Target architecture:** Microservices (.NET 9 + YARP API Gateway + Vue 3 + SQL Server + messaging + LLM + shared vector storage)\
**Current architecture status:** Initial service-host extraction; see implementation baseline below\
**Primary Users:** Management, MIS, Finance, Operations, IT, Branch/Regional Users\
**Implementation status reviewed:** 2026-11-14

---

## Current Implementation & Architecture Baseline

This document defines the desired microservice architecture for **AI MIS & ERP Copilot (v2.0)**. Requirements below describe a target unless explicitly marked as implemented. The repository has an initial service-host split behind YARP; it does not yet implement all target services or data boundaries. Clean Architecture projects are shared code libraries used inside the hosts, not the system's deployment architecture.

| Sprint | Status | Microservices & Domain Scope |
| --- | --- | --- |
| Gateway and service hosts | Partially implemented | YARP gateway plus Identity, Copilot, Knowledge, and Reporting API hosts. Hosts still share Application/Infrastructure assemblies; Identity and Reporting use the shared application database. |
| Identity and access | Partially implemented | JWT login, users, roles, branch assignment, and menus are hosted by Identity. Region/workstation hierarchy and service-owned identity persistence are not implemented. |
| Copilot and Text-to-SQL | Partially implemented | Copilot host, LLM integration, SQL parsing/validation, read-only execution, and schema metadata exist. Polly resilience, unified cross-database views, persistent conversation history, and feedback triage are not implemented. |
| Gateway security and audit | Partially implemented | Gateway and services validate JWTs; gateway rate limiting and downstream authorization are configured. There is no broker-backed AuditService. |
| Knowledge / RAG | Partially implemented | Knowledge host supports authenticated document handling and grounded answers. Its vector index and document metadata use local file storage, not SQL-backed shared vector storage. |
| Reporting | Partially implemented | Reporting host provides MIS reports and user-scoped Excel/CSV import. Saved reports, exports, scheduling, and email delivery are not implemented. |
| Loan proposals | Partially implemented | Authenticated loan proposal summary is hosted in Reporting and enforces branch-claim scoping/date limits. The Vue analysis page still displays static sample rows. |

### Key Architectural & Operational Resolutions (v2.0)

* **Microservice Decomposition (`Section 13 & 14`):** The gateway and four feature API hosts are now separate deployable projects. Shared application/infrastructure libraries and databases are transitional dependencies; full data isolation is not complete.
* **Loan Proposal Integration (`FR-021`):** The Reporting host contains an authenticated summary endpoint with branch claim checks and a 45-day range guard. The frontend page still contains sample records and is not live-integrated.
* **Database Migrations (`NFR-005`):** SQL scripts under `backend/database/Tables/` remain manually applied. Per-service DbUp migrations are a future requirement.
* **Multi-Database Query Abstraction (`Section 2.1`):** The repository does not currently define `vw_AI_BranchPerformanceUnified`; approved unified views remain a target design.
* **Scalable Vector RAG (`NFR-004`):** The Knowledge host currently uses local file-based document and vector storage. Shared SQL vector persistence and horizontal multi-instance safety remain future work.

---

# 1. Executive Summary

**AI MIS & ERP Copilot** is an AI-powered enterprise analytics platform that allows authorized users to interact with ERP/MIS data using natural language.

Instead of manually writing SQL queries, navigating multiple legacy screens, or requesting customized reports from IT/MIS teams, users can ask questions such as:

* *"Show me the total loan collection for August 2026."*
* *"Which branches have collection below recoverable by more than 10%?"*
* *"Show me the top 10 branches based on savings collection."*
* *"Why is Branch 0212 showing high overdue?"*

The platform routes the request through the API Gateway to `AI.MIS.CopilotService`, which interprets business intent, retrieves schema metadata and policy context from `AI.MIS.KnowledgeService`, generates and AST-validates a read-only SQL query, injects mandatory branch/region filters, executes it against read-only ERP datasets, and returns tables, charts, downloadable exports, and AI-generated explanations.

---

# 2. Business Analysis (BA)

## 2.1 Business Problem

Current ERP/MIS environments commonly face the following challenges:

1. **Data Access Complexity:** Users must navigate multiple modules and screens to assemble operational metrics.
2. **Dependency on IT/MIS:** Business users rely heavily on technical staff for ad-hoc SQL queries and custom reports.
3. **SQL Knowledge Barrier:** Complex analytical questions require T-SQL proficiency.
4. **Report Turnaround Time:** Manual report preparation delays management intervention.
5. **Raw Data Without Context:** Traditional reports output numbers without explaining anomalies or root causes.
6. **Fragmented ERP Databases:** Enterprise data spans multiple logical databases:
   * `ERP_MIS`
   * `ERP_HRM`
   * `ERP_ACCOUNTING`
   * `ERP_FMS`
   * `ERP_INV`
   * `ERP_SECURITY`

**Target architectural requirement:** Rather than allowing AI-generated queries to perform arbitrary cross-database joins, curated read-only abstraction views inside `ERP_MIS` (such as `vw_AI_BranchPerformanceUnified`) should pre-join approved entities. This view is not currently present in repository SQL scripts.

---

# 3. Proposed Business & Microservices Solution

```text
                        +---------------------------+
                        |   Vue 3 Web Application   |
                        |       (frontend/)         |
                        +-------------+-------------+
                                      | HTTPS / JWT
                                      v
                        +---------------------------+
                        |     AI.MIS.Gateway        |
                        |  (YARP Reverse Proxy)     |
                        | - Rate Limiting           |
                        | - JWT Signature Check     |
                        | - Path-Based Routing      |
                        +-------------+-------------+
                                      |
        +----------------+------------+------------+----------------+
        |                |                         |                |
        v                v                         v                v
+---------------+ +---------------+       +----------------+ +---------------+
| Identity &    | | Copilot &     |       | Knowledge &    | | Reporting &   |
| Menu Service  | | Text-to-SQL   |       | RAG Service    | | Loan Services |
+-------+-------+ +-------+-------+       +--------+-------+ +-------+-------+
        |                 |                        |                 |
        |                 +-----------+------------+                 |
        |                             | (gRPC / REST / Events)       |
        v                             v                              v
+---------------+           +-------------------+          +-------------------+
| Service-Owned |           | SQL Server ERP    |          | Message Broker    |
| Config DBs    |           | Read-Only Replica |          | (RabbitMQ/Kafka)  |
| (DbUp Managed)|           | & Unified Views   |          | -> AuditService   |
+---------------+           +-------------------+          +-------------------+
```

---

# 4. Business Objectives

1. Eliminate routine dependency on IT/MIS teams for ad-hoc data queries.
2. Enable natural-language querying across loan, savings, branch, and HR-linked MIS metrics.
3. Automate recurring report generation, export (`XLSX`, `PDF`, `CSV`), and scheduled email delivery.
4. Provide live, validated operational dashboards (including Loan Proposal Analysis) with zero static mock data.
5. Deliver AI explanations that clearly separate deterministic SQL calculations from analytical interpretations.
6. Isolate AI, RAG, and reporting workloads via microservices so external LLM latency never impacts core ERP/MIS responsiveness.
7. Enforce zero-trust RBAC and row-level branch/region filtering across every microservice.
8. Maintain an immutable, event-driven audit trail (`AuditService`) and continuous MIS feedback loop (`CopilotFeedback`).

---

# 5. Business Benefits

## Existing Manual Process

```text
User -> Request MIS Report -> MIS / IT -> Understand Requirement -> Write SQL -> Test SQL -> Generate Report -> Email User
```

## Proposed Microservices Copilot Process

```text
User -> Vue UI -> YARP Gateway -> CopilotService (Intent + RAG + Safe SQL + Branch Scope) -> Read-Only ERP DB -> Table / Chart / AI Summary -> Export (XLSX/PDF/CSV) / Save / Schedule -> Async Audit Event
```

Key benefits include:

* Sub-5-second self-service answers for standard operational questions
* Fault isolation via microservice boundaries and Polly circuit breakers
* Horizontal scalability for 1,000+ concurrent users via stateless services and SQL-backed vector search
* Governed business definitions curated by MIS Administrators via feedback triage

---

# 6. Stakeholder Analysis

| Stakeholder | Responsibility |
| --- | --- |
| Management | View executive insights, live loan proposal summaries, and scheduled reports |
| MIS Team | Curate `AISchemaMetadata` and `AIBusinessTerm`, validate SQL patterns, and triage user feedback |
| Finance | Financial analysis and accounting reconciliation |
| Operations | Branch/regional portfolio monitoring and overdue tracking |
| Branch Users | Branch-restricted operational queries and workbook analysis |
| IT Team | Container orchestration, gateway routing, and deployment operations |
| Database Administrator | Read-only ERP replica governance, unified view tuning, and backup policies |
| Business Analyst | Domain rules and reporting requirement alignment |
| Developer | Microservice development, contract versioning, and DbUp migration maintenance |
| System Administrator | Identity, system roles, branch/region assignments, and menu visibility management |

---

# 7. User Roles & Security Boundaries

## 7.1 System Administrator
* Manages users, system roles, and branch/region assignments in `AI.MIS.IdentityService`
* Configures LLM/Embedding provider credentials and secrets
* Manages navigation catalog and custom menu-only visibility roles (`GET/POST/PUT /api/menus/*`)
* Inspects compliance logs from `AI.MIS.AuditService`

## 7.2 MIS Administrator
* Maintains approved database schema metadata (`AISchemaMetadata`) and business definitions (`AIBusinessTerm`)
* Reviews and resolves flagged AI responses in the MIS Feedback Triage queue (`CopilotFeedback`)

## 7.3 Management User
* Queries cross-branch/regional performance, views live Loan Proposal Analysis, saves reusable reports, schedules automated deliveries, and exports `XLSX`/`PDF`/`CSV` reports

## 7.4 Branch User
Strictly scoped to assigned branch data across all services:

```text
Branch User -> JWT Claim (BranchCode = 0212) -> Gateway & Service Auth Middleware -> Mandatory SQL Filter (BranchCode = '0212')
```

## 7.5 Regional/Area User
Scoped to assigned Area/Region hierarchies derived from workstation assignments.

## 7.6 Menu Visibility Roles (`BR-006`)
Custom menu-only roles managed by `AI.MIS.IdentityService` control sidebar navigation rendering in `AI.MIS.Web`. They are strictly decoupled from system security roles and never grant access to downstream microservice APIs or data.

---

# 8. Major Use Cases

## UC-01: Ask Business Question
**Actor:** Authorized User\
**Example:** *"What was the total collection during August 2026?"*\
**Flow:**
1. `AI.MIS.Web` sends prompt with JWT to `AI.MIS.Gateway` -> `AI.MIS.CopilotService`.
2. `CopilotService` loads conversation history and calls `KnowledgeService` (via gRPC/HTTP) for relevant business terms.
3. LLM generates structured intent and candidate T-SQL.
4. SQL AST parser validates read-only `SELECT` compliance and table/view whitelisting.
5. Middleware injects mandatory `BranchCode`/`RegionCode` predicates.
6. Query executes against read-only ERP SQL Server; results, chart config, and AI summary are persisted to `CopilotMessage` and published to `AuditService`.

## UC-02: Branch Performance Analysis (Cross-DB Unified View)
**User:** *"Show branches where collection is less than 90% of recoverable."*\
**Output:** Queries `vw_AI_BranchPerformanceUnified` (joining `ERP_MIS` and `ERP_HRM`) within the user's scope:

| Branch | Recoverable | Collection | Gap | Collection % |
| --- | ---: | ---: | ---: | ---: |
| 0212 | 12,500,000 | 9,800,000 | 2,700,000 | 78.40% |
| 0215 | 8,200,000 | 7,000,000 | 1,200,000 | 85.37% |

## UC-03: Automated Visualization
Identifies dimensions, measures, and time series to render dependency-free SVG/CSS Bar, Line, Donut charts, and KPI cards.

## UC-04: Explain Report
Generates structured narratives distinguishing deterministic calculated values from AI interpretations and anomaly highlights.

## UC-05: Persistent Multi-Turn Conversation History
Users resume prior sessions from the sidebar (`CopilotConversation`) and ask contextual follow-ups (*"What about July?"*).

## UC-06: Secure Report Export (`XLSX`, `CSV`, `PDF`)
`AI.MIS.ReportingService` re-validates the underlying SQL and re-injects the requesting user's live branch/region claims before generating `.xlsx` (ClosedXML), `.csv`, or `.pdf` (QuestPDF) streams.

## UC-07: Saved & Scheduled Reports
Users save reusable queries (`SavedReport`) and configure CRON schedules (`ScheduledReport`) executed by a background worker in `AI.MIS.ReportingService` to email reports to authorized recipients.

## UC-08: Manage Menu Visibility
Administrators configure sidebar routes, ordering, and custom menu-only role mappings via `AI.MIS.IdentityService`.

## UC-09: Query Accuracy Feedback & MIS Triage
Users rate responses (`Correct` 👍 / `Incorrect` 👎 with category and comment). MIS Administrators triage flagged queries to refine `AIBusinessTerm` and `AISchemaMetadata`.

---

# 9. Business Rules

* **BR-001 — Zero-Trust Authorization:** Every microservice independently validates JWT signatures and RBAC policies; internal network trust is never assumed.
* **BR-002 — Deterministic Branch & Region Scoping:** Organizational filters are injected programmatically into SQL execution and domain queries; LLM-generated `WHERE` clauses are never relied upon for security.
* **BR-003 — Read-Only ERP Access:** All AI and reporting connections use a least-privilege read-only SQL Server login; `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`, `EXEC`, and `MERGE` are blocked at both parser and database levels.
* **BR-004 — AST SQL Validation:** Every dynamic or saved query passes SQL AST parsing and approved table/view whitelisting prior to execution.
* **BR-005 — Governed Business Terminology:** Domain terms (`Recoverable`, `Collection`, `Outstanding`, `Overdue`, `Disbursement`, `Savings`, `Center`, `Member`, `Loan Product`, `Branch`, `EIN`) are governed in `AIBusinessTerm`.
* **BR-006 — Menu Visibility Is Not Authorization:** Menu-only roles affect frontend navigation rendering exclusively.
* **BR-007 — Live Operational Data Integrity:** Operational screens (including Loan Proposal Analysis) must never render mock data; non-branch queries spanning > 45 days require branch-level or proposed-amount scope (`FR-021`).

---

# 10. Software Requirements Specification (SRS)

## Functional Requirements

* **FR-001 Authentication (`Partially implemented`):** JWT login is issued by the Identity host; the gateway and extracted API hosts validate JWTs. Workstation/region claims and service-owned identity storage remain unimplemented.
* **FR-002 User & Scope Management (`Partially implemented`):** User/role management and branch assignments exist. Region assignments and workstation hierarchy mapping are not implemented.
* **FR-003 Natural Language Query (`Implemented`):** Natural-language query interface in `AI.MIS.Web` routed to `AI.MIS.CopilotService`.
* **FR-004 AI Intent Detection (`Implemented`):** Structured extraction of `Intent`, `Entity`, `Metric`, `Date`, `Filters`, `Grouping`, and `Sorting`.
* **FR-005 Curated Schema & Cross-DB Views (`Partially implemented`):** Controlled schema metadata exists. The unified view `vw_AI_BranchPerformanceUnified` is not present in repository SQL scripts.
* **FR-006 SQL Generation (`Implemented`):** Context-aware T-SQL generation using approved metadata, business terms, and conversation history.
* **FR-007 SQL AST Validation (`Implemented`):** Parser validation, read-only enforcement, whitelist verification, and row-level security injection.
* **FR-008 Read-Only Query Execution (`Implemented`):** Executed against read-only ERP SQL Server connections with strict command timeouts.
* **FR-009 Result Visualization (`Implemented`):** Automated selection of Data Tables, KPI Cards, and SVG/CSS Bar, Line, and Pie/Donut charts.
* **FR-010 AI Summary (`Implemented`):** Grounded executive summary generated from executed query metrics.
* **FR-011 Persistent Conversation History (`Not implemented`):** The current Copilot host does not persist multi-turn conversations.
* **FR-012 User Feedback & MIS Triage (`Not implemented`):** Feedback capture and triage are target requirements.
* **FR-013 Event-Driven Query Audit (`Not implemented`):** Query audit is currently application logging. RabbitMQ/Kafka and `AI.MIS.AuditService` are not present.
* **FR-014 Saved Reusable Reports (`Not implemented`):** Saved reports and runtime SQL re-validation are target requirements.
* **FR-015 Scheduled Reports (`Not implemented`):** CRON scheduling and email report delivery are target requirements.
* **FR-016 Scalable SQL Vector RAG (`Partially implemented`):** The Knowledge host supports document retrieval using local file storage. Message-queue ingestion, SQL vector persistence, and horizontal vector search are not implemented.
* **FR-017 Menu Catalog (`Implemented`):** Managed in `AI.MIS.IdentityService` (`MenuItems`).
* **FR-018 Menu Visibility & Custom Menu Roles (`Implemented`):** Role-filtered navigation endpoint (`GET /api/menus`) decoupled from backend API authorization.
* **FR-019 Administrator Menu Management UI (`Implemented`):** Admin screen in `frontend/` for menu catalog and custom menu-role assignments.
* **FR-020 Excel/CSV Workbook Import & Analysis (`Implemented`):** `AI.MIS.ReportingService` supports authenticated imports (`.xlsx`, `.xls`, `.xlsm`, `.csv` up to 20 MB, 20,000 rows, 100 columns), persisted per user with dynamic grouping and aggregation.
* **FR-021 Loan Proposal Summary API & Live Analysis View (`Partially implemented`):** The Reporting host exposes `/api/LoanProposal` with JWT branch-claim scoping and a 45-day range guard. The frontend still uses static sample rows; a separate Loan Analytics host and live view integration are not implemented.

---

# 11. Scalable Microservice RAG Architecture

`AI.MIS.KnowledgeService` decouples document ingestion from query-time vector retrieval and stores embeddings in SQL Server so multiple service replicas scale horizontally without file locks:

```text
Document Upload (PDF/DOCX/XLSX/MD/TXT) -> KnowledgeService API -> Message Broker (DocumentUploadedEvent)
                                                                          |
                                                                          v
SQL Server (KnowledgeEmbedding) <- Embedding Generation <- Chunking Worker
       |
       v (Internal gRPC / REST call from CopilotService)
Role-Filtered Vector Similarity Search -> Grounded Policy Citations -> LLM Answer
```

---

# 12. Non-Functional Requirements

The performance and scale values below are target objectives; they have not been demonstrated by repository benchmarks.

* **NFR-001 Performance:** Standard queries and cached RAG lookups `< 5s`; complex analytical queries and multi-page PDF/Excel exports `< 15s`.
* **NFR-002 Security:** TLS everywhere, JWT validation at gateway and services, least-privilege read-only ERP database logins, AST SQL validation, rate limiting, and immutable async audit logging.
* **NFR-003 Fault Isolation & Reliability:** Polly retry and circuit-breaker policies isolate external LLM outages inside `CopilotService` and `KnowledgeService`, ensuring `IdentityService`, `ReportingService`, and `LoanAnalyticsService` remain 100% available.
* **NFR-004 Horizontal Scalability:** Stateless microservices and SQL Server-backed vector storage scale horizontally across Docker/Kubernetes replicas to support 1,000+ concurrent users.
* **NFR-005 Maintainability & Automated Deployment:** Clean Architecture inside each microservice, shared contracts (`AI.MIS.Contracts`), automated **DbUp** database migrations at service startup, and automated unit/integration test suites in `tests/`.

---

# 13. Technology Stack

* **API Gateway:** YARP (Yet Another Reverse Proxy) on .NET 9 / Nginx
* **Backend Microservices:** .NET 9, ASP.NET Core Web API, gRPC (internal service-to-service), C#, Clean Architecture, Polly, DbUp
* **Messaging & Event Bus:** RabbitMQ / Apache Kafka (MassTransit) for async RAG ingestion, scheduled jobs, and audit logging
* **Frontend:** Vue 3, TypeScript, Fetch API, Dependency-free SVG/CSS charts (`frontend/`)
* **Reporting & Exports:** ClosedXML (`.xlsx`), QuestPDF (`.pdf`), CsvHelper (`.csv`)
* **Databases:**
  * **ERP Read-Only Replica (SQL Server):** `ERP_MIS`, `ERP_HRM`, `ERP_ACCOUNTING`, and unified read-only views (`vw_AI_BranchPerformanceUnified`)
  * **Service-Owned Databases (SQL Server):** Isolated schemas/DBs per microservice (`IdentityDb`, `CopilotDb`, `KnowledgeDb` with native `VECTOR` support, `ReportingDb`, `AuditDb`)
* **AI Provider:** OpenAI-compatible LLM & Embeddings APIs with Structured Outputs

---

# 14. Repository & Microservices Solution Structure

The current repository structure is shown below. The future services listed elsewhere in this target SRS are not all implemented.

```text
AI-MIS-Copilot/
├── AI.MIS.Copilot.sln
├── AI_MIS_ERP_Copilot_BA_SRS.md
├── README.md
├── backend/
│   ├── AI.MIS.Gateway/                      # YARP API gateway
│   ├── AI.MIS.IdentityService/              # Auth, users, roles, and menus host
│   ├── AI.MIS.CopilotService/               # Copilot query host
│   ├── AI.MIS.KnowledgeService/             # Document/RAG host
│   ├── AI.MIS.ReportingService/             # MIS reports, workbook imports, loan proposal host
│   ├── AI.MIS.HttpApi/                      # Shared controller assembly during extraction
│   ├── AI.MIS.ServiceDefaults/              # Shared host setup/controller filtering
│   ├── AI.MIS.Api/                          # Compatibility API host
│   ├── AI.MIS.Application/                  # Shared application library (transitional)
│   ├── AI.MIS.Domain/                       # Shared domain library (transitional)
│   ├── AI.MIS.Infrastructure/               # Shared adapters (transitional)
│   ├── database/                            # Manually applied SQL scripts
│   └── deploy/docker-compose.yml             # Multi-container local deployment
├── frontend/                                # Vue 3 + TypeScript SPA
│   └── src/                                 # pages, components, API configuration, styles
└── tests/
    ├── AI.MIS.UnitTests/                    # Existing unit tests
    └── AI.MIS.UnitTests.Extended/            # Existing extended unit tests
```

---

# 15. Gateway & Microservice API Design

All external client requests enter via `AI.MIS.Gateway` (`/api/*`) and route to the owning microservice:

| Gateway Route Prefix | Target Microservice | Key Endpoints |
| --- | --- | --- |
| `/api/auth/*`, `/api/users/*` | `AI.MIS.IdentityService` | `POST /api/auth/login`, User & Branch/Region assignment APIs |
| `/api/menus/*` | `AI.MIS.IdentityService` | `GET /api/menus`, `GET/POST/PUT/DELETE /api/menus/manage`, custom menu-role APIs |
| `/api/copilot/*` | `AI.MIS.CopilotService` | `POST /api/copilot/query`, `GET /api/copilot/conversations`, `POST /api/copilot/feedback`, `PUT /api/copilot/feedback/{id}/resolve` |
| `/api/documents/*` | `AI.MIS.KnowledgeService` | Document list, upload, index, search, and answer endpoints |
| `/api/misReports/*`, `/api/excelImports/*` | `AI.MIS.ReportingService` | MIS dashboard/report and authenticated Excel/CSV import endpoints |
| `/api/LoanProposal/*` | `AI.MIS.ReportingService` | Loan proposal summary API (branch claim scoping and 45-day date guard) |

---

# 16. Database Ownership & Automated Migrations

Database ownership is transitional. Identity and Reporting use the configured shared application database. Reporting and Copilot use configured MIS/ERP SQL connections. Knowledge stores documents and vectors in local files. SQL scripts under `backend/database/Tables/` are applied manually; per-service databases and DbUp migrations are not yet implemented.

---

# 17. AI Schema Metadata & Prompt Strategy

`CopilotService` injects curated metadata (`AISchemaMetadata`) mapping business terms to approved tables and cross-database views:

```json
{
  "table": "vw_AI_BranchPerformanceUnified",
  "description": "Unified read-only view combining branch recoverable, loan collection, gap, collection rate, and active field officer counts across ERP_MIS and ERP_HRM.",
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

System instructions enforce strict `SELECT`-only generation, adherence to `AIBusinessTerm` rules, clarification prompts when user queries are ambiguous, and structured JSON responses.

---

# 18. Zero-Trust Microservices Security Pipeline

```text
Vue Client -> YARP Gateway (Rate Limit + JWT Check) -> CopilotService (JWT validation)
    -> LLM SQL Proposal -> SQL AST Parser -> Read-Only Check -> Table/View Whitelist
    -> Query Authorization -> Query Timeout -> Read-Only SQL Login
    -> Application audit logging
```

---

# 19. Microservices Completion Checklist

```text
[✓] YARP gateway and four independently deployable API hosts
[~] Identity, users, roles, and menus extracted; persistence remains shared
[~] Copilot query API extracted; conversation history and feedback are absent
[ ] Unified cross-database read-only SQL views
[~] Knowledge API extracted; vector and document storage remains local files
[~] Excel/CSV import; saved reports, exports, scheduling, and email remain absent
[~] Loan proposal API exists in Reporting; frontend page still uses sample rows
[ ] Broker-backed audit service
[ ] Service-owned databases and automated migrations
[ ] Service-specific integration and contract test projects
```

---

# 20. Acceptance Criteria

1. **Implemented:** Requests can enter through the YARP gateway and route to the extracted API hosts.
2. **Implemented:** Extracted service hosts validate JWTs independently.
3. **Implemented:** Copilot SQL is parsed, validated as read-only, and authorized before execution.
4. **Implemented:** Authenticated users can import Excel/CSV workbooks and retrieve their own saved import analysis.
5. **Partial:** An authenticated loan proposal summary endpoint exists; the Vue analysis page remains sample data.
6. **Not implemented:** Service-owned database isolation, DbUp migrations, broker-backed audit, SQL vector storage, saved report/export/schedule workflows, and live loan-page integration.

---

# 21. Conclusion

**AI MIS & ERP Copilot (v2.0)** is the target microservice design. The repository now has an initial YARP gateway and four separately deployable API hosts, while shared code libraries and database dependencies remain. Service-owned data, broker-backed audit/indexing, vector-store scalability, remaining reporting features, and live loan-page integration are still required before the target architecture is complete or production-scale claims are justified.
