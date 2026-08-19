using ErpPersonelLeaveSystem.Data;
using ErpPersonelLeaveSystem.models;
using ErpPersonelLeaveSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpPersonelLeaveSystem.Controllers;

public class CompanyLoginRequest
{
    public string CompanyCode { get; set; } = string.Empty;
    public string EmployeeIdOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class SuperAdminLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private static readonly string[] AdminDepartmentKeywords =
    {
        "yönetim", "yonetim", "insan kaynakları", "insan kaynaklari", "ik", "ceo"
    };

    private readonly ErpDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<Employee> _employeeHasher = new();
    private readonly PasswordHasher<Company> _companyHasher = new();

    public AuthController(ErpDbContext context, ITokenService tokenService, IConfiguration configuration)
    {
        _context = context;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    private static string ResolveRole(string department)
    {
        var dept = (department ?? string.Empty).Trim().ToLower();
        return AdminDepartmentKeywords.Any(k => dept.Contains(k)) ? "Admin" : "Employee";
    }

    // ŞİRKET GİRİŞİ: Şirket Kodu + Personel ID/E-Posta + Şifre (POST /api/auth/login)
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] CompanyLoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CompanyCode) || string.IsNullOrWhiteSpace(request.EmployeeIdOrEmail) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { Message = "Şirket kodu, personel ID/e-posta ve şifre zorunludur." });

            var code = request.CompanyCode.Trim();
            var company = await _context.companies.FirstOrDefaultAsync(c => c.CompanyCode == code);
            if (company == null)
                return Unauthorized(new { Message = "Şirket kodu bulunamadı." });

            if (!company.IsActive)
                return Unauthorized(new { Message = "Bu şirketin lisansı aktif değil." });

            var identifier = request.EmployeeIdOrEmail.Trim();
            Employee? employee;

            if (int.TryParse(identifier, out var employeeId))
            {
                employee = await _context.employees.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.CompanyId == company.Id && e.Id == employeeId);
            }
            else
            {
                var lowered = identifier.ToLower();
                employee = await _context.employees.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.CompanyId == company.Id && e.Email != null && e.Email.ToLower() == lowered);
            }

            if (employee == null || string.IsNullOrEmpty(employee.PasswordHash))
                return Unauthorized(new { Message = "Personel bulunamadı veya şifre tanımlı değil." });

            var verifyResult = _employeeHasher.VerifyHashedPassword(employee, employee.PasswordHash, request.Password);
            if (verifyResult == PasswordVerificationResult.Failed)
                return Unauthorized(new { Message = "Şifre hatalı." });

            var role = ResolveRole(employee.Department);
            var token = _tokenService.GenerateEmployeeToken(company.Id, employee.Id, employee.Name, role);

            return Ok(new
            {
                Success = true,
                Message = $"🔑 Hoş geldiniz, {employee.Name}!",
                Token = token,
                Employee = new
                {
                    Id = employee.Id,
                    Name = employee.Name,
                    Department = employee.Department,
                    Role = role,
                    WorkStatus = (int)employee.WorkStatus,
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    CompanyCode = company.CompanyCode
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Giriş işlemi başarısız.", Error = ex.Message });
        }
    }

    // SÜPER ADMİN GİRİŞİ (POST /api/auth/superadmin-login)
    [HttpPost("superadmin-login")]
    public IActionResult SuperAdminLogin([FromBody] SuperAdminLoginRequest request)
    {
        try
        {
            var configuredEmail = _configuration["SuperAdmin:Email"];
            var configuredHash = _configuration["SuperAdmin:PasswordHash"];

            if (string.IsNullOrEmpty(configuredEmail) || string.IsNullOrEmpty(configuredHash))
                return StatusCode(500, new { Message = "Süper Admin yapılandırması eksik." });

            if (!string.Equals(request.Email?.Trim(), configuredEmail, StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { Message = "E-posta veya şifre hatalı." });

            var verifyResult = _companyHasher.VerifyHashedPassword(new Company(), configuredHash, request.Password ?? string.Empty);
            if (verifyResult == PasswordVerificationResult.Failed)
                return Unauthorized(new { Message = "E-posta veya şifre hatalı." });

            var token = _tokenService.GenerateSuperAdminToken(configuredEmail);
            return Ok(new { Success = true, Token = token, Message = "🔑 Süper Admin girişi başarılı." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Giriş işlemi başarısız.", Error = ex.Message });
        }
    }
}
