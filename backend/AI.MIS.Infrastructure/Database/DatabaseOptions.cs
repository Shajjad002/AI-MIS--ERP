namespace AI.MIS.Infrastructure.Database;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";
    public string ErpReadOnlyDatabase { get; init; } = string.Empty;
    public string ApplicationDatabase { get; init; } = string.Empty;
    public string MISDbConnection { get; init; } = string.Empty;
    public int MISReportCommandTimeoutSeconds { get; init; } = 300;
    public int CommandTimeoutSeconds { get; init; } = 15;
}
