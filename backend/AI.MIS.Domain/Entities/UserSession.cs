using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace AI.MIS.Domain.Entities;

public class OperationPermission
{
    public string ScreenOperationCode { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
}

public class Module
{
    public string Name { get; set; } = string.Empty;
}

public class Notification_Info
{
    public string Message { get; set; } = string.Empty;
}

public class PermissionBaseResponse : BaseResponse
{
    public object? Data { get; set; }
}

public enum RoleList
{
    MicrofinanceProgramAdmin,
    SMEProgramAdmin
}

public class UserSession
{
    public string EIN { get; set; }
    public string Username { get; set; }
    public string UserType { get; set; }
    public string Name { get; set; }
    public string OfficialMobileNo { get; set; }
    public string Email { get; set; }
    public string WorkStationNo { get; set; }
    public string BaseWorkStationNo { get; set; }
    public string WorkStationDetails { get; set; }
    public string WorkStationType { get; set; }
    public DateTime WorkStationSystemDay { get; set; }
    public string WorkStationDayStatus { get; set; }
    public string ProgramCode { get; set; }
    public string DivisionCode { get; set; }
    public string ZoneCode { get; set; }
    public string RegionCode { get; set; }
    public string AreaCode { get; set; }
    public string BranchCode { get; set; }
    public string BranchName { get; set; }
    public string DepartmentCode { get; set; }
    public string DepartmentName { get; set; }
    public string PostCode { get; set; }
    public string PostName { get; set; }
    public string DesignationCode { get; set; }
    public string DesignationName { get; set; }
    public string HealthCenterCode { get; set; }
    public string HealthCenterName { get; set; }
    public string PhotoURL { get; set; }

    public IList<string> Roles { get; set; } = new List<string>();
    public IList<OperationPermission> OperationPermissionList { get; set; } = new List<OperationPermission>();
    public IList<Module> ModuleList { get; set; } = new List<Module>();
    public IList<Notification_Info> NotificationList { get; set; } = new List<Notification_Info>();
    public bool IsProduction { get; set; }
    public string GenericPostCode { get; set; }
    public bool IsHoliday { get; set; }
    public bool IsFirstTimeLogin { get; set; }
    public string TotalJob { get; set; }
    public bool IsFaceRecognitionEnabled { get; set; }

    public void Init(IEnumerable<Claim> claimList, DateTime? systemDay, string dayStatus, string isProduction)
    {
        IsProduction = isProduction == "1" ? true : false;
        Username = claimList.FirstOrDefault(s => s.Type == nameof(Username)).Value;
        UserType = claimList.FirstOrDefault(s => s.Type == nameof(UserType)).Value;
        EIN = claimList.FirstOrDefault(s => s.Type == nameof(EIN)).Value;
        Name = claimList.FirstOrDefault(s => s.Type == ClaimTypes.Name).Value;
        PhotoURL = claimList.FirstOrDefault(s => s.Type == nameof(PhotoURL))?.Value;
        OfficialMobileNo = claimList.FirstOrDefault(s => s.Type == ClaimTypes.MobilePhone)?.Value;
        Email = claimList.FirstOrDefault(s => s.Type == ClaimTypes.Email)?.Value;
        WorkStationNo = claimList.FirstOrDefault(s => s.Type == nameof(WorkStationNo)).Value;
        BaseWorkStationNo = claimList.FirstOrDefault(s => s.Type == nameof(BaseWorkStationNo)).Value;
        WorkStationDetails = claimList.FirstOrDefault(s => s.Type == nameof(WorkStationDetails))?.Value;
        WorkStationType = claimList.FirstOrDefault(s => s.Type == nameof(WorkStationType)).Value;
        IsHoliday = Convert.ToBoolean(claimList.FirstOrDefault(s => s.Type == nameof(IsHoliday)).Value);
        IsFirstTimeLogin = Convert.ToBoolean(claimList.FirstOrDefault(s => s.Type == nameof(IsFirstTimeLogin)).Value);

        // TotalJob = claimList.FirstOrDefault(s => s.Type == nameof(TotalJob)).Value;

        if (systemDay == null)
        {
            var sysDay = claimList.FirstOrDefault(s => s.Type == nameof(WorkStationSystemDay));
            if (sysDay != null && !string.IsNullOrWhiteSpace(sysDay.Value))
            {
                WorkStationSystemDay = Convert.ToDateTime(sysDay.Value);
            }
        }
        else
        {
            WorkStationSystemDay = Convert.ToDateTime(systemDay);
        }

        if (string.IsNullOrWhiteSpace(dayStatus))
        {
            WorkStationDayStatus = claimList.FirstOrDefault(s => s.Type == nameof(WorkStationDayStatus))?.Value;
        }
        else
        {
            WorkStationDayStatus = dayStatus;
        }

        //var systemDay = claimList.FirstOrDefault(s => s.Type == nameof(WorkStationSystemDay));
        //if (systemDay != null && !string.IsNullOrWhiteSpace(systemDay.Value))
        //{
        //    if(WorkStationSystemDay != null && WorkStationSystemDay <= Convert.ToDateTime(systemDay.Value))
        //    {
        //        WorkStationSystemDay = Convert.ToDateTime(systemDay.Value);
        //        //WorkStationDayStatus = claimList.FirstOrDefault(s => s.Type == nameof(WorkStationDayStatus))?.Value;
        //    }
        //    var workStationStatus = claimList.FirstOrDefault(s => s.Type == nameof(WorkStationDayStatus))?.Value;
        //    if (string.IsNullOrWhiteSpace(WorkStationDayStatus))
        //    {
        //        WorkStationDayStatus = workStationStatus;
        //    }
        //}

        ProgramCode = claimList.FirstOrDefault(s => s.Type == nameof(ProgramCode)).Value;
        DivisionCode = claimList.FirstOrDefault(s => s.Type == nameof(DivisionCode))?.Value;
        ZoneCode = claimList.FirstOrDefault(s => s.Type == nameof(ZoneCode))?.Value;
        RegionCode = claimList.FirstOrDefault(s => s.Type == nameof(RegionCode))?.Value;
        AreaCode = claimList.FirstOrDefault(s => s.Type == nameof(AreaCode))?.Value;
        BranchCode = claimList.FirstOrDefault(s => s.Type == nameof(BranchCode))?.Value;
        if (claimList.FirstOrDefault(s => s.Type == nameof(BranchName)) != null) BranchName = claimList.FirstOrDefault(s => s.Type == nameof(BranchName)).Value;
        DepartmentCode = claimList.FirstOrDefault(s => s.Type == nameof(DepartmentCode))?.Value;
        DepartmentName = claimList.FirstOrDefault(s => s.Type == nameof(DepartmentName))?.Value;
        PostCode = claimList.FirstOrDefault(s => s.Type == nameof(PostCode))?.Value;
        PostName = claimList.FirstOrDefault(s => s.Type == nameof(PostName))?.Value;
        GenericPostCode = claimList.FirstOrDefault(s => s.Type == nameof(GenericPostCode))?.Value;
        DesignationCode = claimList.FirstOrDefault(s => s.Type == nameof(DesignationCode))?.Value;
        DesignationName = claimList.FirstOrDefault(s => s.Type == nameof(DesignationName))?.Value;
        HealthCenterCode = claimList.FirstOrDefault(s => s.Type == nameof(HealthCenterCode))?.Value;
        HealthCenterName = claimList.FirstOrDefault(s => s.Type == nameof(HealthCenterName))?.Value;
        Roles = claimList.Where(w => w.Type == ClaimTypes.Role).Select(s => s.Value).ToList();
        if (claimList.FirstOrDefault(s => s.Type == nameof(IsFaceRecognitionEnabled)) != null)
        {
            IsFaceRecognitionEnabled = Convert.ToBoolean(claimList.FirstOrDefault(s => s.Type == nameof(IsFaceRecognitionEnabled)).Value);
        }
    }

    //public void UpdateSessionDayInformation(DateTime systemDay, string dayStatus)
    //{
    //    if (systemDay != null)
    //    {
    //        WorkStationSystemDay = Convert.ToDateTime(systemDay);
    //    }

    //    WorkStationDayStatus = dayStatus;

    //    //var loginIdentity = ClaimsPrincipal.Current.Identities.Where(s => s.Name == "Login Information").FirstOrDefault();
    //    //var loginClaims = loginIdentity.Claims.ToList();

    //    //var workStationSystemDay = loginClaims.FirstOrDefault(s => s.Type == nameof(UserSession.WorkStationSystemDay));
    //    //var workStationDayStatus = loginClaims.FirstOrDefault(s => s.Type == nameof(UserSession.WorkStationDayStatus));
    //    //if (workStationSystemDay != null)
    //    //{
    //    //    loginIdentity.RemoveClaim(workStationSystemDay);
    //    //}
    //    //if (workStationDayStatus != null)
    //    //{
    //    //    loginIdentity.RemoveClaim(workStationDayStatus);
    //    //}
    //    //loginClaims.Add(new Claim("WorkStationSystemDay", systemDay == null || systemDay == DateTime.MinValue ? "" : systemDay.ToShortDateString()));
    //    //loginClaims.Add(new Claim("WorkStationDayStatus", string.IsNullOrWhiteSpace(dayStatus) ? "" : dayStatus));
    //}

    public PermissionBaseResponse CheckPermission(string operationCode/*,Enum permissionName*/)
    {
        //if(permissionName==)
        var permissionCheck = OperationPermissionList.Any(s => s.ScreenOperationCode == operationCode);
        //var result = OperationPermissionList.FirstOrDefault(d => d.ScreenOperationCode == operationCode && d.RoleCode == Roles.Contains(RoleList.MicrofinanceProgramAdmin).ToString());
        var result = OperationPermissionList.FirstOrDefault(d => d.ScreenOperationCode == operationCode && (d.RoleCode == RoleList.MicrofinanceProgramAdmin.ToString() || d.RoleCode == RoleList.SMEProgramAdmin.ToString()));
        return new PermissionBaseResponse()
        {
            Data = result,
            IsSuccessful = permissionCheck,
            Message = permissionCheck ? "Authorized" : "You are not authorized to do this operation."
        };
    }

    public BaseResponse HavePermission(string operationCode)
    {
        if (OperationPermissionList.Any(s => s.ScreenOperationCode == operationCode))
        {
            return new BaseResponse()
            {
                IsSuccessful = true,
                Message = $"You are not authorized for this operation."
            };
        }
        return new BaseResponse()
        {
            IsSuccessful = false,
            Message = $"You are not authorized for this operation."
        };
    }
}

public enum OperationAction
{

    CanCreate,
    CanModify,
    CanDelete,
    CanRead
}

