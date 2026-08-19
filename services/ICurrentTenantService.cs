namespace ErpPersonelLeaveSystem.Services;

public interface ICurrentTenantService
{
    int? CompanyId { get; }
    int? EmployeeId { get; }
    string? Role { get; }
    bool IsSuperAdmin { get; }
    bool IsAuthenticated { get; }
}
