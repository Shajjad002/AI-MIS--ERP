using AI.MIS.Application.Copilot;
using AI.MIS.Application.Copilot.Models;

namespace AI.MIS.Infrastructure.AI;

public sealed class InMemorySchemaMetadataProvider : ISchemaMetadataProvider
{
    private static readonly IReadOnlyList<SchemaTable> Schema =
    [
        new("LoanRecoverable", "Approved monthly loan recoverable amounts.", [new("BranchCode", "Unique branch code", "nvarchar"), new("AccountNo", "Loan account number", "nvarchar"), new("RecoverableAmount", "Amount due for collection", "decimal"), new("RecoverableDate", "Recoverable date", "date")]),
        new("LoanCollection", "Approved loan collection transactions.", [new("BranchCode", "Unique branch code", "nvarchar"), new("AccountNo", "Loan account number", "nvarchar"), new("CollectionAmount", "Collected loan amount", "decimal"), new("CollectionDate", "Collection transaction date", "date")]),
        new("Branch", "Approved branch master data.", [new("BranchCode", "Unique branch code", "nvarchar"), new("BranchName", "Branch display name", "nvarchar"), new("RegionCode", "Assigned region", "nvarchar")])
    ];

    public Task<IReadOnlyList<SchemaTable>> GetApprovedSchemaAsync(CancellationToken cancellationToken = default) => Task.FromResult(Schema);
}
