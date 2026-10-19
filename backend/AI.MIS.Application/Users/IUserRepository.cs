using AI.MIS.Domain.Entities;

namespace AI.MIS.Application.Users;

public interface IUserRepository
{
    Task<AuthenticatedUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default);
}

public sealed record AuthenticatedUser(
    Guid Id,
    string UserName,
    string PasswordHash,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> BranchCodes);
