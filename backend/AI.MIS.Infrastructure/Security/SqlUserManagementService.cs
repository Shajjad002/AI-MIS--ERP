using AI.MIS.Application.Users;
using AI.MIS.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace AI.MIS.Infrastructure.Security;

public sealed class SqlUserManagementService(IOptions<DatabaseOptions> options) : IUserManagementService
{
    public async Task<IReadOnlyList<ManagedUser>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = """
            SELECT u.Id, u.UserName, u.DisplayName, u.Email, u.IsActive, CONVERT(bit, CASE WHEN u.ProfileImage IS NULL THEN 0 ELSE 1 END), r.Name, uba.BranchCode
            FROM dbo.Users u
            LEFT JOIN dbo.UserRoles ur ON ur.UserId = u.Id
            LEFT JOIN dbo.Roles r ON r.Id = ur.RoleId
            LEFT JOIN dbo.UserBranchAccess uba ON uba.UserId = u.Id
            ORDER BY u.UserName;
            """;
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new Dictionary<Guid, UserBuilder>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetGuid(0);
            if (!users.TryGetValue(id, out var user))
                users[id] = user = new UserBuilder(id, reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetBoolean(5));
            if (!reader.IsDBNull(6)) user.Roles.Add(reader.GetString(6));
            if (!reader.IsDBNull(7)) user.BranchCodes.Add(reader.GetString(7));
        }
        return users.Values.Select(user => user.Build()).ToArray();
    }

    public async Task<ManagedUser> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.UserName) || string.IsNullOrWhiteSpace(command.Password))
            throw new ArgumentException("Username and password are required.");
        if (command.Password.Length < 8) throw new ArgumentException("Password must be at least 8 characters.");
        var role = ValidateRole(command.Role);
        var id = Guid.NewGuid();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            const string userSql = """
                INSERT INTO dbo.Users (Id, UserName, DisplayName, Email, PasswordHash, ProfileImage, ProfileImageContentType)
                VALUES (@Id, @UserName, @DisplayName, @Email, @PasswordHash, @ProfileImage, @ProfileImageContentType);
                """;
            await using (var insertUser = new SqlCommand(userSql, connection, transaction))
            {
                insertUser.Parameters.AddWithValue("@Id", id);
                insertUser.Parameters.AddWithValue("@UserName", command.UserName.Trim());
                insertUser.Parameters.AddWithValue("@DisplayName", command.DisplayName.Trim());
                insertUser.Parameters.AddWithValue("@Email", command.Email.Trim());
                insertUser.Parameters.AddWithValue("@PasswordHash", PasswordHash.Create(command.Password));
                insertUser.Parameters.Add("@ProfileImage", SqlDbType.VarBinary, -1).Value = command.ProfileImage is null ? DBNull.Value : command.ProfileImage;
                insertUser.Parameters.Add("@ProfileImageContentType", SqlDbType.NVarChar, 50).Value = (object?)command.ProfileImageContentType ?? DBNull.Value;
                await insertUser.ExecuteNonQueryAsync(cancellationToken);
            }
            var roleId = await GetRoleIdAsync(connection, transaction, role, cancellationToken);
            await ExecuteAsync(connection, transaction, "INSERT INTO dbo.UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);", cancellationToken, ("@UserId", id), ("@RoleId", roleId));
            if (role == "Branch User")
                foreach (var branch in command.BranchCodes.Where(branch => !string.IsNullOrWhiteSpace(branch)).Select(branch => branch.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                    await ExecuteAsync(connection, transaction, "INSERT INTO dbo.UserBranchAccess (UserId, BranchCode) VALUES (@UserId, @BranchCode);", cancellationToken, ("@UserId", id), ("@BranchCode", branch));
            await transaction.CommitAsync(cancellationToken);
            return new ManagedUser(id, command.UserName.Trim(), command.DisplayName.Trim(), command.Email.Trim(), true, [role], command.BranchCodes, command.ProfileImage is not null);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("That username or email already exists.", exception);
        }
    }

    public async Task<ProfileImage?> GetProfileImageAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = "SELECT ProfileImage, ProfileImageContentType FROM dbo.Users WHERE Id = @Id;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", userId);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0) || reader.IsDBNull(1)) return null;
        return new ProfileImage((byte[])reader.GetValue(0), reader.GetString(1));
    }

    public async Task UpdateAccessAsync(Guid userId, UpdateUserAccessCommand command, CancellationToken cancellationToken = default)
    {
        var role = ValidateRole(command.Role);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, transaction, "UPDATE dbo.Users SET IsActive = @IsActive WHERE Id = @Id;", cancellationToken, ("@IsActive", command.IsActive), ("@Id", userId));
        var roleId = await GetRoleIdAsync(connection, transaction, role, cancellationToken);
        await ExecuteAsync(connection, transaction, "DELETE FROM dbo.UserRoles WHERE UserId = @Id;", cancellationToken, ("@Id", userId));
        await ExecuteAsync(connection, transaction, "INSERT INTO dbo.UserRoles (UserId, RoleId) VALUES (@Id, @RoleId);", cancellationToken, ("@Id", userId), ("@RoleId", roleId));
        await ExecuteAsync(connection, transaction, "DELETE FROM dbo.UserBranchAccess WHERE UserId = @Id;", cancellationToken, ("@Id", userId));
        if (role == "Branch User")
            foreach (var branch in command.BranchCodes.Where(branch => !string.IsNullOrWhiteSpace(branch)).Select(branch => branch.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                await ExecuteAsync(connection, transaction, "INSERT INTO dbo.UserBranchAccess (UserId, BranchCode) VALUES (@Id, @BranchCode);", cancellationToken, ("@Id", userId), ("@BranchCode", branch));
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(options.Value.ApplicationDatabase);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
    private static string ValidateRole(string role) => role is "Administrator" or "MIS Analyst" or "Branch User" ? role : throw new ArgumentException("Unsupported role.");
    private static async Task<Guid> GetRoleIdAsync(SqlConnection connection, SqlTransaction transaction, string role, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT Id FROM dbo.Roles WHERE Name = @Name;", connection, transaction);
        command.Parameters.AddWithValue("@Name", role);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : throw new InvalidOperationException($"Role '{role}' is not configured.");
    }
    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction transaction, string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    private sealed class UserBuilder(Guid id, string userName, string displayName, string email, bool isActive, bool hasProfileImage)
    {
        public HashSet<string> Roles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> BranchCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ManagedUser Build() => new(id, userName, displayName, email, isActive, Roles.ToArray(), BranchCodes.ToArray(), hasProfileImage);
    }
}
