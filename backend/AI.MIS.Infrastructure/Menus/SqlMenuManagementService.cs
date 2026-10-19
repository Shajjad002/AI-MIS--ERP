using AI.MIS.Application.Menus;
using AI.MIS.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace AI.MIS.Infrastructure.Menus;

public sealed class SqlMenuManagementService(IOptions<DatabaseOptions> options) : IMenuManagementService
{
    private static readonly HashSet<string> PageKeys = ["home", "visualization", "documents", "users"];
    private static readonly HashSet<string> IconNames =
    [
        "overview", "dashboard", "report", "explore", "cohorts", "studio", "anomaly",
        "forecast", "recommendation", "ask", "source", "model", "quality", "governance",
        "alert", "metric", "settings", "users"
    ];

    public async Task<IReadOnlyList<MenuDefinition>> ListVisibleAsync(
        Guid userId,
        IReadOnlyCollection<string> identityRoles,
        CancellationToken cancellationToken = default)
    {
        var menus = await ListManagedAsync(cancellationToken);
        var userMenuRoles = await ListUserMenuRolesAsync(userId, cancellationToken);
        var roleNames = new HashSet<string>(identityRoles.Concat(userMenuRoles), StringComparer.OrdinalIgnoreCase);
        var isAdministrator = identityRoles.Contains("Administrator", StringComparer.OrdinalIgnoreCase);

        return menus
            .Where(menu => menu.IsEnabled && (isAdministrator || menu.RoleNames.Any(roleNames.Contains)))
            .OrderBy(menu => menu.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(menu => menu.SortOrder)
            .ThenBy(menu => menu.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<MenuDefinition>> ListManagedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = """
            SELECT m.Id, m.Label, m.Section, m.PageKey, m.Icon, m.SortOrder, m.IsEnabled, r.RoleName
            FROM dbo.MenuItems m
            LEFT JOIN dbo.MenuItemRoles r ON r.MenuItemId = m.Id
            ORDER BY m.Section, m.SortOrder, m.Label, r.RoleName;
            """;
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new Dictionary<Guid, MenuBuilder>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetGuid(0);
            if (!items.TryGetValue(id, out var item))
            {
                items[id] = item = new MenuBuilder(
                    id,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetInt32(5),
                    reader.GetBoolean(6));
            }
            if (!reader.IsDBNull(7))
                item.RoleNames.Add(reader.GetString(7));
        }
        return items.Values.Select(item => item.Build()).ToArray();
    }

    public async Task<IReadOnlyList<string>> ListRoleNamesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = """
            SELECT RoleName
            FROM dbo.MenuRoleDefinitions
            ORDER BY IsSystemRole DESC, RoleName;
            """;
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var roleNames = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            roleNames.Add(reader.GetString(0));
        return roleNames;
    }

    public async Task CreateRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var normalizedName = ValidateRoleName(roleName);
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = """
            IF EXISTS (SELECT 1 FROM dbo.MenuRoleDefinitions WHERE RoleName = @RoleName)
                THROW 50001, 'A menu role with this name already exists.', 1;
            INSERT INTO dbo.MenuRoleDefinitions (RoleName, IsSystemRole) VALUES (@RoleName, 0);
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@RoleName", SqlDbType.NVarChar, 100).Value = normalizedName;
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627 or 50001)
        {
            throw new InvalidOperationException("A menu role with this name already exists.", exception);
        }
    }

    public async Task<IReadOnlyList<string>> ListUserMenuRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = """
            SELECT umr.RoleName
            FROM dbo.UserMenuRoles umr
            INNER JOIN dbo.MenuRoleDefinitions r ON r.RoleName = umr.RoleName
            WHERE umr.UserId = @UserId AND r.IsSystemRole = 0
            ORDER BY umr.RoleName;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var roleNames = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            roleNames.Add(reader.GetString(0));
        return roleNames;
    }

    public async Task SetUserMenuRolesAsync(Guid userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roleNames);
        var normalizedNames = roleNames.Select(ValidateRoleName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var userCheck = new SqlCommand("SELECT COUNT(1) FROM dbo.Users WHERE Id = @UserId;", connection, transaction))
        {
            userCheck.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            if ((int)await userCheck.ExecuteScalarAsync(cancellationToken) == 0)
                throw new KeyNotFoundException("The user was not found.");
        }

        foreach (var roleName in normalizedNames)
        {
            await using var roleCheck = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.MenuRoleDefinitions WHERE RoleName = @RoleName AND IsSystemRole = 0;",
                connection,
                transaction);
            roleCheck.Parameters.Add("@RoleName", SqlDbType.NVarChar, 100).Value = roleName;
            if ((int)await roleCheck.ExecuteScalarAsync(cancellationToken) == 0)
                throw new ArgumentException($"Unknown custom menu role '{roleName}'.");
        }

        await ExecuteAsync(connection, transaction, "DELETE FROM dbo.UserMenuRoles WHERE UserId = @UserId;", cancellationToken,
            ("@UserId", SqlDbType.UniqueIdentifier, userId));
        foreach (var roleName in normalizedNames)
        {
            await ExecuteAsync(connection, transaction,
                "INSERT INTO dbo.UserMenuRoles (UserId, RoleName) VALUES (@UserId, @RoleName);",
                cancellationToken,
                ("@UserId", SqlDbType.UniqueIdentifier, userId),
                ("@RoleName", SqlDbType.NVarChar, roleName));
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Guid> CreateMenuAsync(MenuWriteRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = ValidateRequest(request);
        var id = Guid.NewGuid();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ValidateMenuRolesAsync(connection, transaction, normalized.RoleNames, cancellationToken);
        const string insertSql = """
            INSERT INTO dbo.MenuItems (Id, Label, Section, PageKey, Icon, SortOrder, IsEnabled)
            VALUES (@Id, @Label, @Section, @PageKey, @Icon, @SortOrder, @IsEnabled);
            """;
        await using (var command = new SqlCommand(insertSql, connection, transaction))
        {
            AddMenuParameters(command, id, normalized);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertMenuRolesAsync(connection, transaction, id, normalized.RoleNames, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    public async Task UpdateMenuAsync(Guid id, MenuWriteRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = ValidateRequest(request);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ValidateMenuRolesAsync(connection, transaction, normalized.RoleNames, cancellationToken);
        const string updateSql = """
            UPDATE dbo.MenuItems
            SET Label = @Label, Section = @Section, PageKey = @PageKey, Icon = @Icon,
                SortOrder = @SortOrder, IsEnabled = @IsEnabled
            WHERE Id = @Id;
            """;
        await using (var command = new SqlCommand(updateSql, connection, transaction))
        {
            AddMenuParameters(command, id, normalized);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
                throw new KeyNotFoundException("The menu entry was not found.");
        }
        await ExecuteAsync(connection, transaction, "DELETE FROM dbo.MenuItemRoles WHERE MenuItemId = @Id;", cancellationToken,
            ("@Id", SqlDbType.UniqueIdentifier, id));
        await InsertMenuRolesAsync(connection, transaction, id, normalized.RoleNames, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteMenuAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand("DELETE FROM dbo.MenuItems WHERE Id = @Id;", connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new KeyNotFoundException("The menu entry was not found.");
    }

    private async Task ValidateMenuRolesAsync(SqlConnection connection, SqlTransaction transaction, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken)
    {
        foreach (var roleName in roleNames)
        {
            await using var command = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.MenuRoleDefinitions WHERE RoleName = @RoleName;",
                connection,
                transaction);
            command.Parameters.Add("@RoleName", SqlDbType.NVarChar, 100).Value = roleName;
            if ((int)await command.ExecuteScalarAsync(cancellationToken) == 0)
                throw new ArgumentException($"Unknown menu access role '{roleName}'.");
        }
    }

    private static async Task InsertMenuRolesAsync(SqlConnection connection, SqlTransaction transaction, Guid menuId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken)
    {
        foreach (var roleName in roleNames)
        {
            await ExecuteAsync(connection, transaction,
                "INSERT INTO dbo.MenuItemRoles (MenuItemId, RoleName) VALUES (@MenuItemId, @RoleName);",
                cancellationToken,
                ("@MenuItemId", SqlDbType.UniqueIdentifier, menuId),
                ("@RoleName", SqlDbType.NVarChar, roleName));
        }
    }

    private static async Task ExecuteAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, SqlDbType Type, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
            command.Parameters.Add(parameter.Name, parameter.Type).Value = parameter.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static MenuWriteRequest ValidateRequest(MenuWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.RoleNames);
        var label = RequireText(request.Label, "Menu label", 100);
        var section = RequireText(request.Section, "Menu section", 100);
        if (request.SortOrder is < 0 or > 100000)
            throw new ArgumentException("Menu sort order must be between 0 and 100000.");
        if (!PageKeys.Contains(request.PageKey))
            throw new ArgumentException("Choose a supported menu destination.");
        if (!IconNames.Contains(request.Icon))
            throw new ArgumentException("Choose a supported menu icon.");
        var roleNames = request.RoleNames.Select(ValidateRoleName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (roleNames.Length == 0)
            throw new ArgumentException("Select at least one role that can see this menu.");
        return request with { Label = label, Section = section, RoleNames = roleNames };
    }

    private static string RequireText(string value, string field, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
            throw new ArgumentException($"{field} is required and must be no longer than {maxLength} characters.");
        return normalized;
    }

    private static string ValidateRoleName(string roleName)
    {
        var normalized = RequireText(roleName, "Role name", 100);
        if (normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ' ' and not '-' and not '_'))
            throw new ArgumentException("Role names may contain letters, numbers, spaces, hyphens, and underscores only.");
        return normalized;
    }

    private static void AddMenuParameters(SqlCommand command, Guid id, MenuWriteRequest request)
    {
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        command.Parameters.Add("@Label", SqlDbType.NVarChar, 100).Value = request.Label;
        command.Parameters.Add("@Section", SqlDbType.NVarChar, 100).Value = request.Section;
        command.Parameters.Add("@PageKey", SqlDbType.NVarChar, 30).Value = request.PageKey;
        command.Parameters.Add("@Icon", SqlDbType.NVarChar, 30).Value = request.Icon;
        command.Parameters.Add("@SortOrder", SqlDbType.Int).Value = request.SortOrder;
        command.Parameters.Add("@IsEnabled", SqlDbType.Bit).Value = request.IsEnabled;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(options.Value.ApplicationDatabase);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private sealed class MenuBuilder(Guid id, string label, string section, string pageKey, string icon, int sortOrder, bool isEnabled)
    {
        public HashSet<string> RoleNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public MenuDefinition Build() => new(id, label, section, pageKey, icon, sortOrder, isEnabled, RoleNames.OrderBy(name => name).ToArray());
    }
}
