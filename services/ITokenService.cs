namespace ErpPersonelLeaveSystem.Services;

public interface ITokenService
{
    string GenerateEmployeeToken(int companyId, int employeeId, string name, string role);
    string GenerateSuperAdminToken(string email);
}
