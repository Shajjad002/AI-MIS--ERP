namespace AI.MIS.Application.Menus;

public interface IMenuManagementService
{
    Task<IReadOnlyList<MenuDefinition>> ListVisibleAsync(Guid userId, IReadOnlyCollection<string> identityRoles, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MenuDefinition>> ListManagedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListRoleNamesAsync(CancellationToken cancellationToken = default);
    Task CreateRoleAsync(string roleName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListUserMenuRolesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetUserMenuRolesAsync(Guid userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default);
    Task<Guid> CreateMenuAsync(MenuWriteRequest request, CancellationToken cancellationToken = default);
    Task UpdateMenuAsync(Guid id, MenuWriteRequest request, CancellationToken cancellationToken = default);
    Task DeleteMenuAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record MenuDefinition(
    Guid Id,
    string Label,
    string Section,
    string PageKey,
    string Icon,
    int SortOrder,
    bool IsEnabled,
    IReadOnlyList<string> RoleNames);

public sealed record MenuWriteRequest(
    string Label,
    string Section,
    string PageKey,
    string Icon,
    int SortOrder,
    bool IsEnabled,
    IReadOnlyList<string> RoleNames);
