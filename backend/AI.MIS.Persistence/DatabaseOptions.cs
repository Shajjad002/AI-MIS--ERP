namespace AI.MIS.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";
    public string ApplicationDatabase { get; init; } = string.Empty;
    public string ErpReadOnlyDatabase { get; init; } = string.Empty;
}
