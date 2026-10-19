namespace AI.MIS.Domain.Entities;

public sealed class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Role> Roles { get; } = new List<Role>();
}
