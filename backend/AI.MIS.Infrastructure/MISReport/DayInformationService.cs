using AI.MIS.Application.Copilot;
using AI.MIS.Application.Copilot.Interface;
using AI.MIS.Application.Copilot.Models;
using AI.MIS.Application.Copilot.SPModels;
using AI.MIS.Application.Copilot.SPModels.MIS;
using AI.MIS.Domain.Entities;
using AI.MIS.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace AI.MIS.Infrastructure.MISReport;

public sealed class DayInformationService(IOptions<DatabaseOptions> options)
    : BaseService<DayInformation>, IDayInformationService
{
    public async Task<IList<WorkStationDayStatusSP>> GetWorkStationDayStatusList(BranchDayStatusViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (viewModel.SystemDay is null)
            throw new ArgumentException("A system day is required to retrieve workstation day status.", nameof(viewModel));

        var connectionString = options.Value.MISDbConnection;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The MIS report database is not configured. Set ConnectionStrings__MISDbConnection.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            $"[ERP_MIS].[dbo].[{MISSPEnum.USP_GetWorkstationDayStatus}]",
            connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = Math.Clamp(options.Value.MISReportCommandTimeoutSeconds, 1, 1800)
        };
        command.Parameters.Add("@ProgramCode", SqlDbType.VarChar, 3).Value =
            string.IsNullOrWhiteSpace(viewModel.ProgramCode) ? DBNull.Value : viewModel.ProgramCode;
        command.Parameters.Add("@SystemDay", SqlDbType.DateTime).Value = viewModel.SystemDay.Value;

        await using var reader = await command.ExecuteReaderAsync();
        var branchOrdinal = reader.GetOrdinal(nameof(WorkStationDayStatusSP.BranchCode));
        var areaOrdinal = reader.GetOrdinal(nameof(WorkStationDayStatusSP.AreaCode));
        var regionOrdinal = reader.GetOrdinal(nameof(WorkStationDayStatusSP.RegionCode));
        var zoneOrdinal = reader.GetOrdinal(nameof(WorkStationDayStatusSP.ZoneCode));
        var statuses = new List<WorkStationDayStatusSP>();

        while (await reader.ReadAsync())
        {
            var status = new WorkStationDayStatusSP
            {
                BranchCode = ReadString(reader, branchOrdinal),
                AreaCode = ReadString(reader, areaOrdinal),
                RegionCode = ReadString(reader, regionOrdinal),
                ZoneCode = ReadString(reader, zoneOrdinal)
            };

            if ((!string.IsNullOrWhiteSpace(viewModel.ZoneCode) && status.ZoneCode != viewModel.ZoneCode) ||
                (!string.IsNullOrWhiteSpace(viewModel.RegionCode) && status.RegionCode != viewModel.RegionCode) ||
                (!string.IsNullOrWhiteSpace(viewModel.AreaCode) && status.AreaCode != viewModel.AreaCode) ||
                (!string.IsNullOrWhiteSpace(viewModel.BranchCode) && status.BranchCode != viewModel.BranchCode))
                continue;

            statuses.Add(status);
        }

        return statuses.OrderBy(status => status.BranchCode).ToList();
    }

    private static string? ReadString(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
}
