# Unit tests

This folder contains isolated unit tests for backend logic. The test project references the existing application and infrastructure projects and does not require a database, API credentials, or external services.

Run from the repository root:

```powershell
dotnet test tests/AI.MIS.UnitTests/AI.MIS.UnitTests.csproj
```

Coverage currently includes approved-schema SQL validation, branch-query authorization, and password hash verification.
