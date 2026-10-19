using AI.MIS.Application.Copilot.Interface;
using AI.MIS.Domain.Entities;
using AI.MIS.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AI.MIS.Infrastructure.MISReport;

public sealed class ProgramService(IOptions<DatabaseOptions> options) : IProgramService
{
    public async Task<ProgramInfo?> GetProgram(string programCode)
    {
        if (string.IsNullOrWhiteSpace(programCode))
            throw new ArgumentException("A program code is required.", nameof(programCode));

        var connectionString = options.Value.MISDbConnection;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The MIS report database is not configured. Set ConnectionStrings__MISDbConnection.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        const string sql = """
            SELECT TOP (1) *
            FROM [ERP_MIS].[dbo].[ProgramInfo]
            WHERE ProgramCode = @ProgramCode AND IsActive = 1 AND IsDeleted = 0;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProgramCode", programCode);
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);

        if (!await reader.ReadAsync())
            return null;

        return new ProgramInfo
        {
            Id = ReadInt64(reader, nameof(ProgramInfo.Id)),
            SetOn = Convert.ToDateTime(reader[nameof(ProgramInfo.SetOn)]),
            SetBy = ReadString(reader, nameof(ProgramInfo.SetBy)),
            IsActive = Convert.ToBoolean(reader[nameof(ProgramInfo.IsActive)]),
            IsDeleted = Convert.ToBoolean(reader[nameof(ProgramInfo.IsDeleted)]),
            ProgramCode = ReadString(reader, nameof(ProgramInfo.ProgramCode)),
            WorkStationNo = ReadString(reader, nameof(ProgramInfo.WorkStationNo)),
            OrganizationCode = ReadString(reader, nameof(ProgramInfo.OrganizationCode)),
            ProgramName = ReadString(reader, nameof(ProgramInfo.ProgramName)),
            Description = ReadString(reader, nameof(ProgramInfo.Description))
        };
    }

    private static long? ReadInt64(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal));
    }

    private static string ReadString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
    }
}
