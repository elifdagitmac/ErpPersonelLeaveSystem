namespace ErpPersonnelLeaveSystem.Controllers;

using ErpPersonelLeaveSystem.Data;
using ErpPersonelLeaveSystem.models;
using ErpPersonelLeaveSystem.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class PersonnelController : ControllerBase
{
    private readonly ErpDbContext _context;
    private readonly ILeaveCalculationService _leaveService;

    public PersonnelController(ErpDbContext context, ILeaveCalculationService leaveService)
    {
        _context = context;
        _leaveService = leaveService;
    }

    // 1. KAPIMIZ: Tüm Personelleri Listele (GET /api/personnel)
    [HttpGet]
    public async Task<IActionResult> GetAllPersonnel()
    {
        try
        {
            var employees = await _context.employees.ToListAsync();
            return Ok(employees);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Personel listesi alınamadı.", Error = ex.Message, Inner = ex.InnerException?.Message });
        }
    }

    // 2. KAPIMIZ: Tek Bir Personeli Getir (GET /api/personnel/5)
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPersonnelById(int id)
    {
        try
        {
            var employee = await _context.employees.FindAsync(id);
            if (employee == null)
                return NotFound("Personel bulunamadı.");

            return Ok(employee);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Personel detayı alınamadı.", Error = ex.Message });
        }
    }

    // 3. KAPIMIZ: Yeni Personel Ekle (POST /api/personnel)
    [HttpPost]
    public async Task<IActionResult> CreatePersonnel([FromBody] Employee employee)
    {
        try
        {
            if (employee == null)
                return BadRequest("Personel verisi boş olamaz.");

            _context.employees.Add(employee);
            await _context.SaveChangesAsync();
            return Ok(employee);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Veritabanına kayıt yapılamadı.", Error = ex.Message, Inner = ex.InnerException?.Message });
        }
    }

    // 4. KAPIMIZ: Personel Bilgilerini Güncelle (PUT /api/personnel/5)
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePersonnel(int id, [FromBody] Employee updatedEmployee)
    {
        try
        {
            var employee = await _context.employees.FindAsync(id);
            if (employee == null)
                return NotFound("Güncellenecek personel bulunamadı.");

            employee.Name = updatedEmployee.Name;
            employee.Department = updatedEmployee.Department;
            employee.MonthlySalary = updatedEmployee.MonthlySalary;
            employee.ExperienceYears = updatedEmployee.ExperienceYears;
            employee.Age = updatedEmployee.Age;
            employee.WorkStatus = updatedEmployee.WorkStatus;

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Personel bilgileri ve PDKS çalışma durumu başarıyla güncellendi.", Employee = employee });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Personel güncellenemedi.", Error = ex.Message });
        }
    }

    // 5. KAPIMIZ: Personel Sil (DELETE /api/personnel/5)
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePersonnel(int id)
    {
        try
        {
            var employee = await _context.employees.FindAsync(id);
            if (employee == null)
                return NotFound("Silinecek personel bulunamadı.");

            _context.employees.Remove(employee);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Personel veritabanından başarıyla silindi." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Personel silinemedi.", Error = ex.Message });
        }
    }

    // 6. KAPIMIZ: Canlı Maaş & İzin Kesintisi Simülatörü (POST /api/personnel/calculate)
    [HttpPost("calculate")]
    public IActionResult CalculatePayroll([FromQuery] decimal monthlySalary, [FromQuery] LeaveType leaveType, [FromQuery] int leaveDays)
    {
        try
        {
            var result = _leaveService.CalculatePayroll(monthlySalary, leaveType, leaveDays);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Maaş hesaplanamadı.", Error = ex.Message });
        }
    }

    // 7. KAPIMIZ: Tüm İzin Geçmişini ve Finansal Detayları Getir (GET /api/personnel/leaves)
    [HttpGet("leaves")]
    public async Task<IActionResult> GetAllLeaveRecords()
    {
        try
        {
            var leaveRecords = await _context.leaveRecords
                .Include(l => l.employee)
                .Select(l => new
                {
                    l.Id,
                    l.employeeId,
                    EmployeeName = l.employee != null ? l.employee.Name : "Bilinmiyor",
                    BaseSalary = l.employee != null ? l.employee.MonthlySalary : 0m,
                    l.LeaveType,
                    l.LeaveDays,
                    l.Note,
                    DeductionAmount = l.CalculatedDeducation,
                    l.FinalSalary
                })
                .ToListAsync();

            return Ok(leaveRecords);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "İzin geçmişi alınamadı.", Error = ex.Message });
        }
    }

    // 8. KAPIMIZ: İzin Talebini Onayla ve Veritabanına Kaydet (POST /api/personnel/add-leave)
    [HttpPost("add-leave")]
    public async Task<IActionResult> AddLeaveRecord([FromBody] LeaveRecord leaveRecord)
    {
        try
        {
            var employee = await _context.employees.FindAsync(leaveRecord.employeeId);
            if (employee == null)
                return NotFound("İzin verilecek personel bulunamadı.");

            var calcResult = _leaveService.CalculatePayroll(employee.MonthlySalary, leaveRecord.LeaveType, leaveRecord.LeaveDays);

            leaveRecord.CalculatedDeducation = calcResult.DeductionAmount;
            leaveRecord.FinalSalary = calcResult.FinalNetSalary;

            _context.leaveRecords.Add(leaveRecord);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "İzin kaydı başarıyla oluşturuldu ve veritabanına işlendi.", Record = leaveRecord });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "İzin kaydı oluşturulamadı.", Error = ex.Message });
        }
    }
}