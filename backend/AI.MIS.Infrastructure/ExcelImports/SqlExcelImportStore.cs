using System.Data;
using System.Text.Json;
using AI.MIS.Application.ExcelImports;
using AI.MIS.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AI.MIS.Infrastructure.ExcelImports;

public sealed class SqlExcelImportStore(IOptions<DatabaseOptions> options) : IExcelImportStore
{
    private const int MaxColumns = 100;
    private const int MaxRows = 20_000;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ExcelImportCreated> CreateAsync(Guid ownerUserId, CreateExcelImportRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var connectionString = options.Value.ApplicationDatabase;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The application database is not configured. Set ConnectionStrings__ApplicationDatabase.");

        var id = Guid.NewGuid();
        var fileName = Path.GetFileName(request.FileName.Trim());
        var worksheetName = request.WorksheetName.Trim();
        var columnsJson = JsonSerializer.Serialize(request.Columns, jsonOptions);
        var importedAt = DateTime.UtcNow;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var command = new SqlCommand("""
                INSERT INTO dbo.ExcelImports (Id, OwnerUserId, FileName, WorksheetName, ColumnsJson, [RowCount], ImportedAt)
                VALUES (@Id, @OwnerUserId, @FileName, @WorksheetName, @ColumnsJson, @RowCount, @ImportedAt);
                """, connection, transaction))
            {
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
                command.Parameters.Add("@OwnerUserId", SqlDbType.UniqueIdentifier).Value = ownerUserId;
                command.Parameters.Add("@FileName", SqlDbType.NVarChar, 260).Value = fileName;
                command.Parameters.Add("@WorksheetName", SqlDbType.NVarChar, 128).Value = worksheetName;
                command.Parameters.Add("@ColumnsJson", SqlDbType.NVarChar, -1).Value = columnsJson;
                command.Parameters.Add("@RowCount", SqlDbType.Int).Value = request.Rows.Count;
                command.Parameters.Add("@ImportedAt", SqlDbType.DateTime2).Value = importedAt;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var rowTable = new DataTable();
            rowTable.Columns.Add("ImportId", typeof(Guid));
            rowTable.Columns.Add("RowNumber", typeof(int));
            rowTable.Columns.Add("RowDataJson", typeof(string));
            for (var index = 0; index < request.Rows.Count; index++)
                rowTable.Rows.Add(id, index + 1, JsonSerializer.Serialize(request.Rows[index], jsonOptions));

            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints, transaction))
            {
                bulkCopy.DestinationTableName = "dbo.ExcelImportRows";
                bulkCopy.BatchSize = 500;
                bulkCopy.BulkCopyTimeout = 120;
                bulkCopy.ColumnMappings.Add("ImportId", "ImportId");
                bulkCopy.ColumnMappings.Add("RowNumber", "RowNumber");
                bulkCopy.ColumnMappings.Add("RowDataJson", "RowDataJson");
                await bulkCopy.WriteToServerAsync(rowTable, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return new ExcelImportCreated(id, fileName, worksheetName, request.Rows.Count, importedAt);
    }

    public async Task<ExcelImportReport?> GetAsync(Guid ownerUserId, Guid importId, CancellationToken cancellationToken = default)
    {
        var connectionString = options.Value.ApplicationDatabase;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The application database is not configured. Set ConnectionStrings__ApplicationDatabase.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        string fileName;
        string worksheetName;
        string columnsJson;
        int rowCount;
        DateTime importedAt;
        await using (var command = new SqlCommand("""
            SELECT FileName, WorksheetName, ColumnsJson, [RowCount], ImportedAt
            FROM dbo.ExcelImports
            WHERE Id = @Id AND OwnerUserId = @OwnerUserId;
            """, connection))
        {
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = importId;
            command.Parameters.Add("@OwnerUserId", SqlDbType.UniqueIdentifier).Value = ownerUserId;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            fileName = reader.GetString(0);
            worksheetName = reader.GetString(1);
            columnsJson = reader.GetString(2);
            rowCount = reader.GetInt32(3);
            importedAt = reader.GetDateTime(4);
        }

        var columns = JsonSerializer.Deserialize<string[]>(columnsJson, jsonOptions) ?? [];
        var rows = new List<JsonElement[]>(rowCount);
        await using (var command = new SqlCommand("""
            SELECT RowDataJson
            FROM dbo.ExcelImportRows
            WHERE ImportId = @ImportId
            ORDER BY RowNumber;
            """, connection))
        {
            command.Parameters.Add("@ImportId", SqlDbType.UniqueIdentifier).Value = importId;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                using var document = JsonDocument.Parse(reader.GetString(0));
                rows.Add(document.RootElement.EnumerateArray().Select(value => value.Clone()).ToArray());
            }
        }

        return new ExcelImportReport(importId, fileName, worksheetName, rowCount, importedAt, columns, rows);
    }

    private static void Validate(CreateExcelImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Length > 260)
            throw new ArgumentException("The workbook file name must be between 1 and 260 characters.");
        if (string.IsNullOrWhiteSpace(request.WorksheetName) || request.WorksheetName.Length > 128)
            throw new ArgumentException("The worksheet name must be between 1 and 128 characters.");
        if (request.Columns is null || request.Columns.Count is < 1 or > MaxColumns)
            throw new ArgumentException($"A worksheet must have between 1 and {MaxColumns} columns.");
        if (request.Columns.Any(column => string.IsNullOrWhiteSpace(column) || column.Length > 128))
            throw new ArgumentException("Column names must be non-empty and at most 128 characters.");
        if (request.Columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Columns.Count)
            throw new ArgumentException("Column names must be unique.");
        if (request.Rows is null || request.Rows.Count is < 1 or > MaxRows)
            throw new ArgumentException($"An import must contain between 1 and {MaxRows:N0} data rows.");
        foreach (var row in request.Rows)
        {
            if (row is null || row.Length != request.Columns.Count)
                throw new ArgumentException("Every data row must have the same number of values as the header row.");
            if (row.Any(value => value.ValueKind is JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Undefined))
                throw new ArgumentException("Workbook cells must contain scalar values.");
        }
    }
}
