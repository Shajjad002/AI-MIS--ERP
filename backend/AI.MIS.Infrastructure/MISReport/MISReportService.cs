using System;
using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using AI.MIS.Application.Copilot;
using AI.MIS.Application.Copilot.Interface;
using AI.MIS.Application.Copilot.Models;
using AI.MIS.Application.Copilot.SPModels;
using AI.MIS.Application.Copilot.SPModels.MIS;
using AI.MIS.Domain.Entities;
using AI.MIS.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AI.MIS.Infrastructure.MISReport;

public class MISReportService : IMISReportService
{
    private readonly IOptions<DatabaseOptions> _options;

    public MISReportService(IOptions<DatabaseOptions> options)
    {
        _options = options;
    }

    public async Task<BaseResponse> GetBranchPortfolioReport(
        ReportParameterViewModel viewModel,
        CancellationToken cancellationToken = default)
    {
        BaseResponse response = new BaseResponse();
        var branchProtfoliosDataList = await GetBranchPortfolioReportData(viewModel, cancellationToken);
        if (!string.IsNullOrWhiteSpace(branchProtfoliosDataList.ErrorMessage))
        {
            response.IsSuccessful = false;
            response.Message = branchProtfoliosDataList.ErrorMessage;
            return response;
        }

        List<BranchProtfolioSP> branchProtfoliosList = branchProtfoliosDataList.BranchProtfolioSP.ToList();
        if (branchProtfoliosList.Count > 0)
        {
            var branchPortfolioViewModel = new BranchPortfolioViewModel();

            branchPortfolioViewModel.BranchProtfolioList = branchProtfoliosList.OrderBy(s => s.BranchCode).OrderBy(s => s.BranchCode).ToList();

            branchPortfolioViewModel.AreaWiseBranchProtfolioList = branchProtfoliosList.GroupBy(g => g.AreaCode)
              .Select(s => new BranchProtfolioSP
              {
                  //ProgramCode = s.FirstOrDefault().ProgramCode,
                  ZoneCode = s.FirstOrDefault().ZoneCode,
                  ZoneName = s.FirstOrDefault().ZoneName,
                  RegionCode = s.FirstOrDefault().RegionCode,
                  RegionName = s.FirstOrDefault().RegionName,
                  AreaCode = s.Key,
                  AreaName = s.FirstOrDefault().AreaName,
                  NoOfCreditOfficer = s.Sum(s => s.NoOfCreditOfficer),
                  NoOfSMEOfficer = s.Sum(s => s.NoOfSMEOfficer),
                  NoOfCenter = s.Sum(s => s.NoOfCenter),
                  Member = s.Sum(s => s.Member),
                  Borrower = s.Sum(s => s.Borrower),
                  OverdueBorrower = s.Sum(s => s.OverdueBorrower),
                  Disbursement = s.Sum(s => s.Disbursement),
                  OutstandingWithServiceCharge = s.Sum(s => s.OutstandingWithServiceCharge),
                  OverdueWithServiceCharge = s.Sum(s => s.OverdueWithServiceCharge),
                  PAR = s.Sum(s => s.PAR),
                  SavingsBalance = s.Sum(s => s.SavingsBalance),
                  PrincipalOutstanding = s.Sum(s => s.PrincipalOutstanding),
                  ServiceChargeOutstanding = s.Sum(s => s.ServiceChargeOutstanding),
                  OverduePrincipal = s.Sum(s => s.OverduePrincipal),
                  Income = s.Sum(s => s.Income),
                  Expense = s.Sum(s => s.Expense),
                  RecoverableAmount = s.Sum(s => s.RecoverableAmount),
                  RegulerRecovered = s.Sum(s => s.RegulerRecovered),
                  BadLoanPrincipalOS = s.Sum(s => s.BadLoanPrincipalOS),
                  PARRatio = s.Sum(s => s.PAR) > 0 && s.Sum(s => s.PrincipalOutstanding) > 0 ? (s.Sum(s => s.PAR) * 100) / s.Sum(s => s.PrincipalOutstanding) : 0,
                  OverdueOutstandingRatio = s.Sum(s => s.OverduePrincipal) > 0 && s.Sum(s => s.PrincipalOutstanding) > 0 ? (s.Sum(s => s.OverduePrincipal) * 100) / s.Sum(s => s.PrincipalOutstanding) : 0,
                  SavingsOutstandingRatio = s.Sum(s => s.SavingsBalance) > 0 && s.Sum(s => s.PrincipalOutstanding) > 0 ? (s.Sum(s => s.SavingsBalance) * 100) / s.Sum(s => s.PrincipalOutstanding) : 0,
                  OTR = s.Sum(s => s.RegulerRecovered) > 0 && s.Sum(s => s.RecoverableAmount) > 0 ? (s.Sum(s => s.RegulerRecovered) * 100) / s.Sum(s => s.RecoverableAmount) : 0,

                  //    GoodLoanPrincipalOSRatio

              }).OrderBy(s => s.AreaCode).ToList();

            branchPortfolioViewModel.RegionWiseBranchProtfolioList = branchProtfoliosList.GroupBy(g => g.RegionCode)
              .Select(s => new BranchProtfolioSP
              {
                  //ProgramCode = s.FirstOrDefault().ProgramCode,
                  ZoneCode = s.FirstOrDefault().ZoneCode,
                  ZoneName = s.FirstOrDefault().ZoneName,
                  RegionCode = s.Key,
                  RegionName = s.FirstOrDefault().RegionName,

                  NoOfCreditOfficer = s.Sum(s => s.NoOfCreditOfficer),
                  NoOfSMEOfficer = s.Sum(s => s.NoOfSMEOfficer),
                  NoOfCenter = s.Sum(s => s.NoOfCenter),
                  Member = s.Sum(s => s.Member),
                  Borrower = s.Sum(s => s.Borrower),
                  OverdueBorrower = s.Sum(s => s.OverdueBorrower),
                  Disbursement = s.Sum(s => s.Disbursement),
                  OutstandingWithServiceCharge = s.Sum(s => s.OutstandingWithServiceCharge),
                  OverdueWithServiceCharge = s.Sum(s => s.OverdueWithServiceCharge),
                  PAR = s.Sum(s => s.PAR),
                  SavingsBalance = s.Sum(s => s.SavingsBalance),
                  PrincipalOutstanding = s.Sum(s => s.PrincipalOutstanding),
                  ServiceChargeOutstanding = s.Sum(s => s.ServiceChargeOutstanding),
                  OverduePrincipal = s.Sum(s => s.OverduePrincipal),
                  Income = s.Sum(s => s.Income),
                  Expense = s.Sum(s => s.Expense),
                  RecoverableAmount = s.Sum(s => s.RecoverableAmount),
                  RegulerRecovered = s.Sum(s => s.RegulerRecovered),
                  BadLoanPrincipalOS = s.Sum(s => s.BadLoanPrincipalOS),
                  PARRatio = s.Sum(s => s.PAR) > 0 && s.Sum(s => s.PrincipalOutstanding) > 0 ? (s.Sum(s => s.PAR) * 100) / s.Sum(s => s.PrincipalOutstanding) : 0,
                  OverdueOutstandingRatio = s.Sum(s => s.OverduePrincipal) > 0 && s.Sum(s => s.PrincipalOutstanding) > 0 ? (s.Sum(s => s.OverduePrincipal) * 100) / s.Sum(s => s.PrincipalOutstanding) : 0,
                  SavingsOutstandingRatio = s.Sum(s => s.SavingsBalance) > 0 && s.Sum(s => s.PrincipalOutstanding) > 0 ? (s.Sum(s => s.SavingsBalance) * 100) / s.Sum(s => s.PrincipalOutstanding) : 0,
                  OTR = s.Sum(s => s.RegulerRecovered) > 0 && s.Sum(s => s.RecoverableAmount) > 0 ? (s.Sum(s => s.RegulerRecovered) * 100) / s.Sum(s => s.RecoverableAmount) : 0,

                  //    GoodLoanPrincipalOSRatio
              }).OrderBy(s => s.RegionCode).ToList();

            branchPortfolioViewModel.ZoneWiseBranchProtfolioList = branchProtfoliosList.GroupBy(g => g.ZoneCode)
              .Select(s => new BranchProtfolioSP
              {
                  //ProgramCode = s.FirstOrDefault().ProgramCode,
                  ZoneName = s.FirstOrDefault().ZoneName,
                  ZoneCode = s.Key,

                  NoOfCreditOfficer = s.Sum(s => s.NoOfCreditOfficer),
                  NoOfSMEOfficer = s.Sum(s => s.NoOfSMEOfficer),
                  NoOfCenter = s.Sum(s => s.NoOfCenter),
                  Member = s.Sum(s => s.Member),
                  Borrower = s.Sum(s => s.Borrower),
                  OverdueBorrower = s.Sum(s => s.OverdueBorrower),
                  Disbursement = s.Sum(s => s.Disbursement),
                  OutstandingWithServiceCharge = s.Sum(s => s.OutstandingWithServiceCharge),
                  OverdueWithServiceCharge = s.Sum(s => s.OverdueWithServiceCharge),
                  PAR = s.Sum(s => s.PAR),
                  SavingsBalance = s.Sum(s => s.SavingsBalance),
                  PrincipalOutstanding = s.Sum(s => s.PrincipalOutstanding),
                  ServiceChargeOutstanding = s.Sum(s => s.ServiceChargeOutstanding),
                  OverduePrincipal = s.Sum(s => s.OverduePrincipal),
                  Income = s.Sum(s => s.Income),
                  Expense = s.Sum(s => s.Expense),
                  RecoverableAmount = s.Sum(s => s.RecoverableAmount),
                  RegulerRecovered = s.Sum(s => s.RegulerRecovered),
                  BadLoanPrincipalOS = s.Sum(s => s.BadLoanPrincipalOS),
                  PARRatio = s.Sum(s => s.PAR) > 0 && s.Sum(s => s.PrincipalOutstanding) > 0 ? (s.Sum(s => s.PAR) * 100) / s.Sum(s => s.PrincipalOutstanding) : 0,
                  OverdueOutstandingRatio = s.Sum(s => s.OverduePrincipal) > 0 && s.Sum(s => s.PrincipalOutstanding) > 0 ? (s.Sum(s => s.OverduePrincipal) * 100) / s.Sum(s => s.PrincipalOutstanding) : 0,
                  SavingsOutstandingRatio = s.Sum(s => s.SavingsBalance) > 0 && s.Sum(s => s.PrincipalOutstanding) > 0 ? (s.Sum(s => s.SavingsBalance) * 100) / s.Sum(s => s.PrincipalOutstanding) : 0,
                  OTR = s.Sum(s => s.RegulerRecovered) > 0 && s.Sum(s => s.RecoverableAmount) > 0 ? (s.Sum(s => s.RegulerRecovered) * 100) / s.Sum(s => s.RecoverableAmount) : 0,

                  //    GoodLoanPrincipalOSRatio

              }).OrderBy(s => s.ZoneCode).ToList();

            if (branchPortfolioViewModel.LendingType == MemberLendingType.Group)
            {
                branchPortfolioViewModel.TotalNoOfOfficer = branchProtfoliosList.Sum(s => s.NoOfCreditOfficer);
            }
            else if (branchPortfolioViewModel.LendingType == MemberLendingType.Individual)
            {
                branchPortfolioViewModel.TotalNoOfOfficer = branchProtfoliosList.Sum(s => s.NoOfSMEOfficer);
            }
            else
            {
                branchPortfolioViewModel.TotalNoOfOfficer = branchProtfoliosList.Sum(s => s.NoOfCreditOfficer + s.NoOfSMEOfficer);
            }

            branchPortfolioViewModel.TotalNoOfCenter = branchProtfoliosList.Sum(s => s.NoOfCenter);
            branchPortfolioViewModel.TotalNoOfCreditOfficer = branchProtfoliosList.Sum(s => s.NoOfCreditOfficer);
            branchPortfolioViewModel.TotalNoOfSMEOfficer = branchProtfoliosList.Sum(s => s.NoOfSMEOfficer);
            branchPortfolioViewModel.TotalMember = branchProtfoliosList.Sum(s => s.Member);
            branchPortfolioViewModel.TotalBorrower = branchProtfoliosList.Sum(s => s.Borrower);
            branchPortfolioViewModel.TotalOverdueBorrower = branchProtfoliosList.Sum(s => s.OverdueBorrower);
            branchPortfolioViewModel.TotalDisbursement = branchProtfoliosList.Sum(s => s.Disbursement);
            branchPortfolioViewModel.TotalOutstandingWithServiceCharge = branchProtfoliosList.Sum(s => s.OutstandingWithServiceCharge);
            branchPortfolioViewModel.TotalOverdueWithServiceCharge = branchProtfoliosList.Sum(s => s.OverdueWithServiceCharge);
            branchPortfolioViewModel.TotalPAR = branchProtfoliosList.Sum(s => s.PAR);
            branchPortfolioViewModel.TotalBadLoanPrincipalOS = branchProtfoliosList.Sum(s => s.BadLoanPrincipalOS);
            branchPortfolioViewModel.TotalSavingsBalance = branchProtfoliosList.Sum(s => s.SavingsBalance);
            branchPortfolioViewModel.TotalPrincipalOutstanding = branchProtfoliosList.Sum(s => s.PrincipalOutstanding);
            branchPortfolioViewModel.TotalServiceChargeOutstanding = branchProtfoliosList.Sum(s => s.ServiceChargeOutstanding);
            branchPortfolioViewModel.TotalOverduePrincipal = branchProtfoliosList.Sum(s => s.OverduePrincipal);
            branchPortfolioViewModel.TotalIncome = branchProtfoliosList.Sum(s => s.Income);
            branchPortfolioViewModel.TotalExpense = branchProtfoliosList.Sum(s => s.Expense);
            branchPortfolioViewModel.TotalRegulerRecovered = branchProtfoliosList.Sum(s => s.RegulerRecovered);
            branchPortfolioViewModel.TotalRecoverableAmount = branchProtfoliosList.Sum(s => s.RecoverableAmount);
            branchPortfolioViewModel.AvaragePARRatio = branchPortfolioViewModel.TotalPAR > 0 && branchPortfolioViewModel.TotalPrincipalOutstanding > 0 ? (branchPortfolioViewModel.TotalPAR * 100) / branchPortfolioViewModel.TotalPrincipalOutstanding : 0;
            branchPortfolioViewModel.AvarageOverdueOutstandingRatio = branchPortfolioViewModel.TotalOverduePrincipal > 0 && branchPortfolioViewModel.TotalPrincipalOutstanding > 0 ? (branchPortfolioViewModel.TotalOverduePrincipal * 100) / branchPortfolioViewModel.TotalPrincipalOutstanding : 0;
            branchPortfolioViewModel.AvarageSavingsOutstandingRatio = branchPortfolioViewModel.TotalSavingsBalance > 0 && branchPortfolioViewModel.TotalPrincipalOutstanding > 0 ? (branchPortfolioViewModel.TotalSavingsBalance * 100) / branchPortfolioViewModel.TotalPrincipalOutstanding : 0;
            branchPortfolioViewModel.AvarageOTR = branchPortfolioViewModel.TotalRegulerRecovered > 0 && branchPortfolioViewModel.TotalRecoverableAmount > 0 ? (branchPortfolioViewModel.TotalRegulerRecovered * 100) / branchPortfolioViewModel.TotalRecoverableAmount : 0;
            branchPortfolioViewModel.TotalZone = branchProtfoliosList.DistinctBy(s => s.ZoneCode).Count();
            branchPortfolioViewModel.TotalRegion = branchProtfoliosList.DistinctBy(s => s.RegionCode).Count();
            branchPortfolioViewModel.TotalArea = branchProtfoliosList.DistinctBy(s => s.AreaCode).Count();
            branchPortfolioViewModel.TotalBranch = branchProtfoliosList.DistinctBy(s => s.BranchCode).Count();
            branchPortfolioViewModel.LendingType = viewModel.LendingType;
            branchPortfolioViewModel.ViewBy = viewModel.ViewBy;
            branchPortfolioViewModel.DataGenerationTime = branchProtfoliosDataList.DataGenerationTime;

            response.IsSuccessful = true;
            response.Message = "Ok";
            response.Data = branchPortfolioViewModel;
        }
        else
        {
            response.IsSuccessful = false;
            response.Message = "No Data Found";
        }
        return response;
    }

    public async Task<decimal> GetTotalDisbursement(
        ReportParameterViewModel viewModel,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _options.Value.MISDbConnection;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The MIS report database is not configured. Set ConnectionStrings__MISDbConnection.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var sessionCommand = new SqlCommand("SET ARITHABORT ON;", connection))
            await sessionCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var command = CreatePortfolioCommand(connection, viewModel);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        var disbursementOrdinal = reader.GetOrdinal(nameof(BranchProtfolioSP.Disbursement));
        decimal totalDisbursement = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(disbursementOrdinal))
                totalDisbursement += Convert.ToDecimal(reader.GetValue(disbursementOrdinal), CultureInfo.InvariantCulture);
        }

        return totalDisbursement;
    }

    private async Task<DashboardDataSP> GetBranchPortfolioReportData(
        ReportParameterViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var connectionString = _options.Value.MISDbConnection;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The MIS report database is not configured. Set ConnectionStrings__MISDbConnection.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var sessionCommand = new SqlCommand("SET ARITHABORT ON;", connection))
            await sessionCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var command = CreatePortfolioCommand(connection, viewModel);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var dataList = await MapReaderAsync<BranchProtfolioSP>(reader, cancellationToken);
        if (!await reader.NextResultAsync(cancellationToken) || !await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("The MIS portfolio procedure returned fewer than two result sets.");

        var dataGenerationTime = Convert.ToDateTime(reader.GetValue(0))
            .ToString("yyyy-MM-dd hh:mm:ss tt");
        return new DashboardDataSP
        {
            BranchProtfolioSP = dataList,
            DataGenerationTime = dataGenerationTime
        };
    }

    private SqlCommand CreatePortfolioCommand(SqlConnection connection, ReportParameterViewModel viewModel)
    {
        var command = new SqlCommand("[ERP_MIS].[dbo].[USP_Portfolio]", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = Math.Clamp(_options.Value.MISReportCommandTimeoutSeconds, 1, 1800)
        };
        command.Parameters.AddWithValue("@ProgramCode", ToSqlValue(viewModel.ProgramCode));
        command.Parameters.AddWithValue("@ZoneCode", ToSqlValue(viewModel.ZoneCode));
        command.Parameters.AddWithValue("@RegionCode", ToSqlValue(viewModel.RegionCode));
        command.Parameters.AddWithValue("@AreaCode", ToSqlValue(viewModel.AreaCode));
        command.Parameters.AddWithValue("@BranchCode", ToSqlValue(viewModel.BranchCode));
        command.Parameters.AddWithValue("@FromDate", ToSqlValue(viewModel.FromDate));
        command.Parameters.AddWithValue("@ToDate", ToSqlValue(viewModel.ToDate));
        command.Parameters.AddWithValue("@LendingType", ToSqlValue(viewModel.LendingType));
        return command;
    }

    private static async Task<List<T>> MapReaderAsync<T>(
        SqlDataReader reader,
        CancellationToken cancellationToken) where T : new()
    {
        var columnOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            columnOrdinals.Add(reader.GetName(ordinal), ordinal);

        var propertyOrdinals = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.SetMethod is not null && columnOrdinals.ContainsKey(property.Name))
            .Select(property => (Property: property, Ordinal: columnOrdinals[property.Name]))
            .ToArray();
        var items = new List<T>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new T();
            foreach (var (property, ordinal) in propertyOrdinals)
            {
                if (reader.IsDBNull(ordinal))
                    continue;

                var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                var value = property.PropertyType == typeof(string)
                    ? Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)
                    : Convert.ChangeType(reader.GetValue(ordinal), targetType, CultureInfo.InvariantCulture);
                property.SetValue(item, value);
            }

            items.Add(item);
        }

        return items;
    }

    private static object ToSqlValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    public Task<LoanTransactionReportSP> GetById(long id) => throw new NotImplementedException();

    public Task<IList<LoanTransactionReportSP>> GetList() =>
        Task.FromResult<IList<LoanTransactionReportSP>>(new List<LoanTransactionReportSP>());

    public Task<IList<LoanTransactionReportSP>> GetList(ISpecification<LoanTransactionReportSP> spec) =>
        Task.FromResult<IList<LoanTransactionReportSP>>(new List<LoanTransactionReportSP>());

    public Task<int> Count(ISpecification<LoanTransactionReportSP> spec) => Task.FromResult(0);

    public Task<LoanTransactionReportSP> Add(LoanTransactionReportSP entity) => throw new NotImplementedException();

    public Task Update(LoanTransactionReportSP entity) => throw new NotImplementedException();

    public Task Delete(long id) => throw new NotImplementedException();

    public Task PermanentDelete(LoanTransactionReportSP entity) => throw new NotImplementedException();

    public Task<IList<LoanTransactionReportSP>> ListAsync() =>
        Task.FromResult<IList<LoanTransactionReportSP>>(new List<LoanTransactionReportSP>());

    public Task<IList<LoanTransactionReportSP>> ListAsync(
        Expression<Func<LoanTransactionReportSP, bool>> criteria,
        params string[] navigations) =>
        Task.FromResult<IList<LoanTransactionReportSP>>(new List<LoanTransactionReportSP>());

    public Task<IList<LoanTransactionReportSP>> ListAsync(ISpecification<LoanTransactionReportSP> spec) =>
        Task.FromResult<IList<LoanTransactionReportSP>>(new List<LoanTransactionReportSP>());

    public Task UpdateAll(List<LoanTransactionReportSP> entity) => throw new NotImplementedException();

    public Task AddAll(List<LoanTransactionReportSP> entity) => throw new NotImplementedException();

    public Task<LoanTransactionReportSP> FirstOrDefaultAsync(
        Expression<Func<LoanTransactionReportSP, bool>> criteria,
        params string[] navigations) =>
        throw new NotImplementedException();

    public Task<LoanTransactionReportSP> FirstOrDefaultAsync(ISpecification<LoanTransactionReportSP> spec) =>
        throw new NotImplementedException();

    public Task UpdateAsync(
        LoanTransactionReportSP entity,
        params Expression<Func<LoanTransactionReportSP, object>>[] properties) =>
        throw new NotImplementedException();
}
