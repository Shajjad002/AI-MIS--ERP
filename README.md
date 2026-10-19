# AI MIS & ERP Copilot

An enterprise-ready foundation for a read-only, natural-language MIS and ERP analytics copilot.

## Project layout

```text
frontend/                 Vue 3 + TypeScript web client
backend/
├── AI.MIS.Api/           ASP.NET Core HTTP API
├── AI.MIS.Application/   Use cases and application contracts
├── AI.MIS.Domain/        Business entities
├── AI.MIS.Infrastructure/External services and data access
├── AI.MIS.Persistence/   Database configuration
└── database/             SQL schema and seed scripts
```

The frontend and backend are separate applications. The backend projects currently form one deployable API with Clean Architecture layers; they are not separate microservices yet.

## Sprint 1 complete

- Clean Architecture .NET solution (API, Application, Domain, Infrastructure, Persistence)
- Vue 3 + TypeScript web starter
- User, role, and branch-access domain/database foundations
- Base API, CORS configuration, health/status endpoints, and JWT login

Configure JWT signing with user secrets or environment variables. User credentials, roles, and branch access are read from the application database; store only PBKDF2 password hashes, never plaintext passwords:

```powershell
$env:Authentication__SigningKey = 'replace-with-at-least-32-random-characters'
$env:ConnectionStrings__ApplicationDatabase = 'Server=...;Database=AiMisErpCopilotDb;Trusted_Connection=True;TrustServerCertificate=True;'
```

Generate a compatible password hash locally without sending the password anywhere:

```powershell
$password = Read-Host 'Admin password' -AsSecureString
$plain = [System.Net.NetworkCredential]::new('', $password).Password
$salt = [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
$key = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($plain, $salt, 100000, [Security.Cryptography.HashAlgorithmName]::SHA256, 32)
"$([Convert]::ToBase64String($salt)).$([Convert]::ToBase64String($key))"
```

Run `backend/database/Seed/003_DemoUsers.sql` against `AiMisErpCopilotDb`, then call `POST /api/auth/login` with a seeded username and password to receive a JWT.

## Sprint 2 complete

- Provider-neutral, OpenAI-compatible LLM client with a 30-second timeout
- Controlled system prompt and JSON-only structured interpretation
- Approved schema metadata provider and API endpoint
- Initial schema metadata database tables
- Interpretation endpoint that never executes its proposed SQL

## Sprint 4 complete

- Vue query form connected to `POST /api/copilot/query`
- Loading and error states
- KPI cards for row count and numeric totals
- Responsive result table
- Dependency-free bar, line, pie, and donut-friendly visualizations
- Result summary with row count and numeric totals

Visualization frontend structure:

```text
frontend/src/
├── pages/VisualizationPage.vue
├── components/visualization/
│   ├── QueryComposer.vue
│   ├── ResultChart.vue
│   ├── ResultSummary.vue
│   └── ResultTable.vue
├── composables/useCopilotQuery.ts
└── types/copilot.ts
```

Authentication frontend structure:

```text
frontend/src/
├── pages/LoginPage.vue
├── composables/useAuth.ts
└── types/auth.ts
```

### Seeded development users

```text
admin / Admin@123
mis.analyst / Mis@123
branch.0212 / Branch@123
```

Change these development-only passwords before any deployment outside local development.

## Sprint 5 complete

- Fixed-window rate limiting on `POST /api/copilot/query` (20 requests/minute)
- Structured query audit logging for success, clarification, rejection, and failure outcomes
- Audit table schema for persisting query metadata without result rows
- Database-backed JWT authentication with role and branch claims
- JWT validation middleware and authorization on the query endpoint
- Branch users must include authorized `BranchCode` equality predicates

Branch-user queries are deliberately limited to a single table and must include an authorized `BranchCode` equality in every `OR` branch. Queries with joins or subqueries are rejected until branch scoping can be enforced independently of generated SQL.

## Sprint 6 complete — RAG

- Authenticated PDF, DOCX, TXT, and Markdown upload with bounded text extraction
- Section/page-aware document chunking with configurable overlap
- OpenAI-compatible batched embedding generation
- Persistent local vector index with cosine similarity search
- Retrieved business-rule and policy passages provided as untrusted context to the LLM
- Grounded answers with server-generated document/page/section citations
- Authenticated document management and RAG question UI
- Admin/MIS Analyst-only document upload, listing, search, and ask endpoints

Configure the embedding provider in user secrets or the deployment secret store. The API must also have the normal `Llm` chat-completions settings configured for grounded answers:

```powershell
dotnet user-secrets set 'Rag:EmbeddingsEndpoint' 'https://api.openai.com/v1/embeddings' --project backend/AI.MIS.Api
dotnet user-secrets set 'Rag:EmbeddingsApiKey' '<provider-key>' --project backend/AI.MIS.Api
dotnet user-secrets set 'Rag:EmbeddingsModel' 'text-embedding-3-small' --project backend/AI.MIS.Api
```

Use the same embedding model and dimensions for indexing and querying. Changing embedding models requires reindexing documents. Documents are stored under `App_Data/documents`, including the local `vectors.json` index; configure `Rag:StoragePath` to a persistent private volume in deployment. Do not expose this directory as a static web directory.

RAG document endpoints:

```text
GET  /api/documents
POST /api/documents/upload (multipart form field: file)
POST /api/documents/{documentId}/index
POST /api/documents/search   { "query": "..." }
POST /api/documents/ask      { "question": "..." }
```

Upload extracts and indexes the document before returning success. If embedding configuration or indexing fails, the file remains listed and can be retried through the index endpoint. Ask responses include citations generated from retrieved passages, not from free-form model citation claims.

### Configure an LLM and read-only ERP database

For local development, keep the OpenAI key in .NET User Secrets rather than in the web app or a checked-in settings file. From the repository root, set it using a secure PowerShell prompt:

```powershell
$secureKey = Read-Host 'OpenAI API key' -AsSecureString
$apiKey = [System.Net.NetworkCredential]::new('', $secureKey).Password
dotnet user-secrets set 'Llm:ApiKey' $apiKey --project backend/AI.MIS.Api
Remove-Variable apiKey, secureKey
dotnet user-secrets set 'Llm:Endpoint' 'https://api.openai.com/v1/chat/completions' --project backend/AI.MIS.Api
dotnet user-secrets set 'Llm:Model' 'gpt-6-astra' --project backend/AI.MIS.Api
```

User Secrets are stored outside the repository and loaded automatically in Development, but are not encrypted; use them for local development only. Use your hosting platform's secret store in deployed environments. The API key is used only by the ASP.NET API. This configures the existing chat-completions client for `POST /api/copilot/interpret`; it does not create an Agents API session.

For a local GPT4All API server, no API key is required:

```powershell
$env:Llm__Endpoint = 'http://localhost:4891/v1/chat/completions'
$env:Llm__Model = 'Phi-3 Mini Instruct'
$env:Llm__ApiKey = ''
$env:ConnectionStrings__ErpReadOnlyDatabase = 'Server=...;Database=...;User Id=...;Password=...;Encrypt=True;'
```

Use `GET /api/schema-metadata` to inspect the only schema sent to the LLM. `POST /api/copilot/interpret` accepts `{ "question": "..." }`; it returns an interpretation and optional proposed SQL, but never executes it.
Use `POST /api/copilot/query` with the same request to validate and execute a proposed query. The query must be one approved, read-only `SELECT` statement and execution is limited to 15 seconds by default. Configure a read-only database principal; application validation is defense in depth, not a replacement for database permissions.

## Run the API

For an existing application database, apply `backend/database/Tables/004_UserProfileImage.sql` before creating users with a profile image. The migration adds nullable image columns and is safe to run more than once. Create User accepts optional JPEG, PNG, or WebP profile images up to 5 MB; stored images are returned only from the authenticated profile-image endpoint.

For menu administration, apply `backend/database/Tables/005_MenuManagement.sql` to the application database before using the menu endpoints or Menu management page. It creates the menu catalog, menu-role definitions, menu visibility grants, and per-user custom menu-role assignments, then seeds the initial navigation entries.

Menu roles only determine which navigation entries are shown. They do not grant authorization to API endpoints: protected API access continues to use the existing Administrator, MIS Analyst, and Branch User JWT roles.

```powershell
dotnet run --project backend/AI.MIS.Api
```

## Run the web client

```powershell
cd frontend
npm.cmd install
node node_modules/vite/bin/vite.js
```

The repository directory includes an `&`, which Windows' npm script wrapper treats as a command separator. Use the direct `node` command above while the project remains at this path.

`ErpReadOnlyDatabase` is deliberately a placeholder. Do not set it to a write-enabled ERP connection.
