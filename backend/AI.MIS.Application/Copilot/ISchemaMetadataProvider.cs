using AI.MIS.Application.Copilot.Models;

namespace AI.MIS.Application.Copilot;

public interface ISchemaMetadataProvider
{
    Task<IReadOnlyList<SchemaTable>> GetApprovedSchemaAsync(CancellationToken cancellationToken = default);
}
