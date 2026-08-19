using ErpPersonelLeaveSystem.Data;
using ErpPersonelLeaveSystem.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpPersonelLeaveSystem.Controllers;

public class CreateCompanyRequest
{
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminName { get; set; } = "Şirket Yöneticisi";
    public string MasterPassword { get; set; } = string.Empty;
}

[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController : ControllerBase
{
    private readonly ErpDbContext _context;
    private readonly PasswordHasher<Company> _companyHasher = new();
    private readonly PasswordHasher<Employee> _employeeHasher = new();

    public SuperAdminController(ErpDbContext context)
    {
        _context = context;
    }

    // TÜM ŞİRKETLERİ LİSTELE (GET /api/superadmin/companies)
    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies()
    {
        try
        {
            var list = await _context.companies.OrderByDescending(c => c.CreatedAt).ToListAsync();
            return Ok(list.Select(c => new { c.Id, c.CompanyCode, c.CompanyName, c.AdminEmail, c.IsActive, c.CreatedAt }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Şirket listesi alınamadı.", Error = ex.Message });
        }
    }

    // YENİ ŞİRKET EKLE + İLK ŞİRKET YÖNETİCİSİNİ OLUŞTUR (POST /api/superadmin/companies)
    [HttpPost("companies")]
    public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CompanyCode) || string.IsNullOrWhiteSpace(request.CompanyName) ||
                string.IsNullOrWhiteSpace(request.AdminEmail) || string.IsNullOrWhiteSpace(request.MasterPassword))
                return BadRequest(new { Message = "Şirket kodu, şirket adı, admin e-postası ve şifre zorunludur." });

            var code = request.CompanyCode.Trim().ToUpper();
            var exists = await _context.companies.AnyAsync(c => c.CompanyCode == code);
            if (exists)
                return BadRequest(new { Message = $"'{code}' şirket kodu zaten kullanılıyor." });

            var company = new Company
            {
                CompanyCode = code,
                CompanyName = request.CompanyName.Trim(),
                AdminEmail = request.AdminEmail.Trim(),
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            company.MasterPasswordHash = _companyHasher.HashPassword(company, request.MasterPassword);

            await _context.companies.AddAsync(company);
            await _context.SaveChangesAsync();

            var adminEmployee = new Employee
            {
                CompanyId = company.Id,
                Name = request.AdminName.Trim(),
                Email = request.AdminEmail.Trim(),
                Department = "Yönetim",
                ExperienceYears = 0,
                EducationLevel = "-",
                Age = 0,
                Gender = "-",
                MonthlySalary = 0,
                WorkStatus = WorkStatusType.Ofiste
            };
            adminEmployee.PasswordHash = _employeeHasher.HashPassword(adminEmployee, request.MasterPassword);

            await _context.employees.AddAsync(adminEmployee);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = $"🏢 '{company.CompanyName}' şirketi ve ilk yöneticisi oluşturuldu.",
                Company = new { company.Id, company.CompanyCode, company.CompanyName, company.AdminEmail, company.IsActive }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Şirket oluşturulamadı.", Error = ex.Message });
        }
    }

    // ŞİRKET LİSANS DURUMUNU AKTİF/PASİF YAP (POST /api/superadmin/companies/{id}/toggle-active)
    [HttpPost("companies/{id}/toggle-active")]
    public async Task<IActionResult> ToggleCompanyActive(int id)
    {
        try
        {
            var company = await _context.companies.FindAsync(id);
            if (company == null) return NotFound(new { Message = "Şirket bulunamadı." });

            company.IsActive = !company.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { Success = true, Message = company.IsActive ? "✅ Lisans aktifleştirildi." : "🔴 Lisans pasifleştirildi.", company.IsActive });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Lisans durumu güncellenemedi.", Error = ex.Message });
        }
    }

    // ŞİRKETİ SİL (DELETE /api/superadmin/companies/{id})
    [HttpDelete("companies/{id}")]
    public async Task<IActionResult> DeleteCompany(int id)
    {
        try
        {
            var company = await _context.companies.FindAsync(id);
            if (company == null) return NotFound(new { Message = "Şirket bulunamadı." });

            var relatedEmployees = await _context.employees.IgnoreQueryFilters().Where(e => e.CompanyId == id).ToListAsync();
            var relatedLeaves = await _context.leaveRecords.IgnoreQueryFilters().Where(l => l.CompanyId == id).ToListAsync();
            var relatedAdvances = await _context.advanceExpenseRecords.IgnoreQueryFilters().Where(a => a.CompanyId == id).ToListAsync();
            var relatedAnnouncements = await _context.announcements.IgnoreQueryFilters().Where(a => a.CompanyId == id).ToListAsync();

            _context.leaveRecords.RemoveRange(relatedLeaves);
            _context.advanceExpenseRecords.RemoveRange(relatedAdvances);
            _context.announcements.RemoveRange(relatedAnnouncements);
            _context.employees.RemoveRange(relatedEmployees);
            _context.companies.Remove(company);

            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = $"🗑️ '{company.CompanyName}' şirketi ve tüm verileri silindi." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Şirket silinemedi.", Error = ex.Message });
        }
    }
}
