using AI.MIS.Application.Users;
using AI.MIS.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AI.MIS.Infrastructure.Security;

public sealed class SqlUserRepository(IOptions<DatabaseOptions> options) : IUserRepository
{
    public async Task<AuthenticatedUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var connectionString = options.Value.ApplicationDatabase;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The application database is not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            SELECT u.Id, u.UserName, u.PasswordHash, r.Name AS RoleName, uba.BranchCode
            FROM dbo.Users AS u
            LEFT JOIN dbo.UserRoles AS ur ON ur.UserId = u.Id
            LEFT JOIN dbo.Roles AS r ON r.Id = ur.RoleId
            LEFT JOIN dbo.UserBranchAccess AS uba ON uba.UserId = u.Id
            WHERE u.UserName = @UserName AND u.IsActive = 1;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserName", userName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Guid? id = null;
        string? actualUserName = null;
        string? passwordHash = null;
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var branches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            id ??= reader.GetGuid(0);
            actualUserName ??= reader.GetString(1);
            passwordHash ??= reader.GetString(2);
            if (!reader.IsDBNull(3)) roles.Add(reader.GetString(3));
            if (!reader.IsDBNull(4)) branches.Add(reader.GetString(4));
        }

        return id is null || actualUserName is null || passwordHash is null
            ? null
            : new AuthenticatedUser(id.Value, actualUserName, passwordHash, roles.ToArray(), branches.ToArray());
    }
}
