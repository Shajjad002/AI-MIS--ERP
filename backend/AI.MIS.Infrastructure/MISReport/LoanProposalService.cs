using AI.MIS.Application.Copilot;
using AI.MIS.Application.Copilot.Interface;
using AI.MIS.Application.Copilot.Models;
using AI.MIS.Application.Copilot.SPModels;
using AI.MIS.Application.Copilot.SPModels.MIS;
using AI.MIS.Domain.Entities;
using AI.MIS.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.MIS.Infrastructure.MISReport
{
    public class LoanProposalService : BaseService<LoanProposal>, ILoanProposalService
    {
        private readonly IOptions<DatabaseOptions> _options;
        public LoanProposalService(IOptions<DatabaseOptions> options)
        {
            _options = options;
        }

        public async Task<IList<LoanProposalSearchSP>> GetLoanProposalSummaryList(LoanProposalSearchModel viewModel)
        {
            var connectionString = _options.Value.MISDbConnection;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("The MIS report database is not configured.");

            await using var connection = new SqlConnection(connectionString);
            await using var command = new SqlCommand(MISSPEnum.USP_GetLoanProposalSearchList.ToString(), connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure,
                CommandTimeout = _options.Value.MISReportCommandTimeoutSeconds
            };

            foreach (var property in typeof(LoanProposalSearchModel).GetProperties())
            {
                if (property.Name is nameof(LoanProposalSearchModel.IsBranchLevel) or nameof(LoanProposalSearchModel.BranchLevelDisbursementRange))
                    continue;
                var value = property.GetValue(viewModel);
                command.Parameters.AddWithValue("@" + property.Name, value ?? DBNull.Value);
            }

            command.Parameters.AddWithValue("@IsBranchLevel", viewModel.IsBranchLevel ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@BranchLevelDisbursementRange", viewModel.BranchLevelDisbursementRange);

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();
            var ordinals = Enumerable.Range(0, reader.FieldCount)
                .ToDictionary(reader.GetName, index => index, StringComparer.OrdinalIgnoreCase);
            var properties = typeof(LoanProposalSearchSP).GetProperties();
            var result = new List<LoanProposalSearchSP>();
            while (await reader.ReadAsync())
            {
                var row = new LoanProposalSearchSP();
                foreach (var property in properties)
                {
                    if (!ordinals.TryGetValue(property.Name, out var ordinal) || await reader.IsDBNullAsync(ordinal))
                        continue;
                    var value = reader.GetValue(ordinal);
                    var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    property.SetValue(row, targetType.IsInstanceOfType(value) ? value : Convert.ChangeType(value, targetType));
                }
                result.Add(row);
            }
            return result;
        }
    }



}
