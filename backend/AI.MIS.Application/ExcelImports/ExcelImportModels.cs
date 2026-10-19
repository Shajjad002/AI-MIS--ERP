using System.Text.Json;

namespace AI.MIS.Application.ExcelImports;

public sealed record CreateExcelImportRequest(
    string FileName,
    string WorksheetName,
    IReadOnlyList<string> Columns,
    IReadOnlyList<JsonElement[]> Rows);

public sealed record ExcelImportCreated(Guid Id, string FileName, string WorksheetName, int RowCount, DateTime ImportedAt);

public sealed record ExcelImportReport(
    Guid Id,
    string FileName,
    string WorksheetName,
    int RowCount,
    DateTime ImportedAt,
    IReadOnlyList<string> Columns,
    IReadOnlyList<JsonElement[]> Rows);

public interface IExcelImportStore
{
    Task<ExcelImportCreated> CreateAsync(Guid ownerUserId, CreateExcelImportRequest request, CancellationToken cancellationToken = default);
    Task<ExcelImportReport?> GetAsync(Guid ownerUserId, Guid importId, CancellationToken cancellationToken = default);
}
