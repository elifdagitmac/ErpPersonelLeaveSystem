namespace ErpPersonnelLeaveSystem.Controllers;


using ErpPersonelLeaveSystem.Data;
using ErpPersonelLeaveSystem.models;
using ErpPersonelLeaveSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// [ApiController]: REST Web API kontrolcüsü olduğunu belirtir.
// [Route]: API adresinin 'api/personnel' olacağını belirler.
[ApiController]
[Route("api/[controller]")]
public class PersonnelController : ControllerBase
{
    private readonly ErpDbContext _context;
    private readonly ILeaveCalculationService _leaveService;

    // Kurucu Metod (Dependency Injection)
    public PersonnelController(ErpDbContext context, ILeaveCalculationService leaveService)
    {
        _context = context;
        _leaveService = leaveService;
    }

    // 1. KAPIMIZ: Tüm Personelleri Listele (GET /api/personnel)
    [HttpGet]
    public async Task<IActionResult> GetAllPersonnel()
    {
        var employees = await _context.employees.ToListAsync();
        return Ok(employees);
    }

    // 2. KAPIMIZ: Tek Bir Personeli Getir (GET /api/personnel/5)
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPersonnelById(int id)
    {
        var employee = await _context.employees.FindAsync(id);
        if (employee == null)
            return NotFound("Personel bulunamadı.");

        return Ok(employee);
    }

    // 3. KAPIMIZ: Yeni Personel Ekle (POST /api/personnel)
    [HttpPost]
    public async Task<IActionResult> CreatePersonnel([FromBody] Employee employee)
    {
        _context.employees.Add(employee);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPersonnelById), new { id = employee.Id }, employee);
    }

    // 4. KAPIMIZ: Canlı Maaş & İzin Kesintisi Simülatörü (POST /api/personnel/calculate)
    [HttpPost("calculate")]
    public IActionResult CalculatePayroll([FromQuery] decimal monthlySalary, [FromQuery] LeaveType leaveType, [FromQuery] int leaveDays)
    {
        var result = _leaveService.CalculatePayroll(monthlySalary, leaveType, leaveDays);
        return Ok(result);
    }

    // 5. KAPIMIZ: İzin Talebini Onayla ve Veritabanına Kaydet (POST /api/personnel/add-leave)
    [HttpPost("add-leave")]
    public async Task<IActionResult> AddLeaveRecord([FromBody] LeaveRecord leaveRecord)
    {
        // Senin modelindeki küçük 'employeeId' ye göre personeli buluyoruz
        var employee = await _context.employees.FindAsync(leaveRecord.employeeId);
        if (employee == null)
            return NotFound("İzin verilecek personel bulunamadı.");

        // Maaş kesintisini canlı hesapla
        var calcResult = _leaveService.CalculatePayroll(employee.MonthlySalary, leaveRecord.LeaveType, leaveRecord.LeaveDays);

        // Senin modelindeki 'CalculatedDeducation' ismine birebir eşitliyoruz
        leaveRecord.CalculatedDeducation = calcResult.DeductionAmount;
        leaveRecord.FinalSalary = calcResult.FinalNetSalary;

        _context.leaveRecords.Add(leaveRecord);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "İzin kaydı başarıyla oluşturuldu ve veritabanına işlendi.", Record = leaveRecord });
    }
}