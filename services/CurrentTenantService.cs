using System.Security.Claims;

namespace ErpPersonelLeaveSystem.Services;

public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public int? CompanyId
    {
        get
        {
            var value = User?.FindFirst("CompanyId")?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public int? EmployeeId
    {
        get
        {
            var value = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Role => User?.FindFirst(ClaimTypes.Role)?.Value;

    public bool IsSuperAdmin => Role == "SuperAdmin";
}
