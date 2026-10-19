namespace AI.MIS.Domain.Entities;

public sealed class BranchAccess
{
    public Guid UserId { get; init; }
    public string BranchCode { get; init; } = string.Empty;
}
