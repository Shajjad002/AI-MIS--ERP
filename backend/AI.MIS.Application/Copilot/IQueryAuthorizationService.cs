namespace AI.MIS.Application.Copilot;

public interface IQueryAuthorizationService
{
    void Validate(string sql, System.Security.Claims.ClaimsPrincipal user);
}
