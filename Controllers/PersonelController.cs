using ErpPersonelLeaveSystem.Data;
using ErpPersonelLeaveSystem.models;
using ErpPersonelLeaveSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpPersonelLeaveSystem.Controllers;

public class CardSwipeRequest
{
    public int EmployeeId { get; set; }
    public string CardUid { get; set; } = string.Empty;
}

public class LeaveNotificationRequest
{
    public int LeaveRecordId { get; set; }
    public string MessageNote { get; set; } = string.Empty;
}

[ApiController]
[Route("api/personnel")]
[Authorize]
public class PersonnelController : ControllerBase
{
    private readonly ErpDbContext _context;
    private readonly ILeaveCalculationService _calculationService;
    private readonly ICurrentTenantService _tenant;
    private readonly PasswordHasher<Employee> _passwordHasher = new();

    public PersonnelController(ErpDbContext context, ILeaveCalculationService calculationService, ICurrentTenantService tenant)
    {
        _context = context;
        _calculationService = calculationService;
        _tenant = tenant;
    }

    private int CompanyId => _tenant.CompanyId ?? 0;
    private int CurrentEmployeeId => _tenant.EmployeeId ?? 0;
    private bool IsAdmin => _tenant.Role == "Admin";

    // 1. TÜM PERSONELLERİ GETİR (GET /api/personnel)
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
            return StatusCode(500, new { Message = "Personel listesi alınamadı.", Error = ex.Message });
        }
    }

    // 2. TEK PERSONEL GETİR (GET /api/personnel/{id})
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPersonnelById(int id)
    {
        try
        {
            var employee = await _context.employees.FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null)
                return NotFound("Personel bulunamadı.");

            return Ok(employee);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Personel detayları alınamadı.", Error = ex.Message });
        }
    }

    // 3. YENİ PERSONEL EKLE (POST /api/personnel)
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreatePersonnel([FromBody] Employee employee)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            employee.CompanyId = CompanyId;

            // Admin bir şifre belirlemezse varsayılan şifre, personelin ID'si olarak atanır (kayıttan sonra belli olur).
            var explicitPassword = employee.Password;
            employee.PasswordHash = _passwordHasher.HashPassword(employee, string.IsNullOrWhiteSpace(explicitPassword) ? "temp" : explicitPassword);
            employee.Password = null;

            await _context.employees.AddAsync(employee);
            await _context.SaveChangesAsync();

            if (string.IsNullOrWhiteSpace(explicitPassword))
            {
                employee.PasswordHash = _passwordHasher.HashPassword(employee, employee.Id.ToString());
                await _context.SaveChangesAsync();
            }

            return Ok(employee);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Personel eklenemedi.", Error = ex.Message });
        }
    }

    // 4. PERSONEL BİLGİLERİNİ GÜNCELLE (PUT /api/personnel/{id})
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePersonnel(int id, [FromBody] Employee updatedEmployee)
    {
        try
        {
            var existingEmployee = await _context.employees.FirstOrDefaultAsync(e => e.Id == id);
            if (existingEmployee == null)
                return NotFound("Güncellenecek personel bulunamadı.");

            existingEmployee.Name = updatedEmployee.Name;
            existingEmployee.Department = updatedEmployee.Department;
            existingEmployee.MonthlySalary = updatedEmployee.MonthlySalary;
            existingEmployee.ExperienceYears = updatedEmployee.ExperienceYears;
            existingEmployee.Age = updatedEmployee.Age;
            existingEmployee.WorkStatus = updatedEmployee.WorkStatus;
            existingEmployee.Email = updatedEmployee.Email;

            if (!string.IsNullOrWhiteSpace(updatedEmployee.Password))
                existingEmployee.PasswordHash = _passwordHasher.HashPassword(existingEmployee, updatedEmployee.Password);

            await _context.SaveChangesAsync();
            return Ok(existingEmployee);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Personel güncellenemedi.", Error = ex.Message });
        }
    }

    // 5. PERSONEL SİL (DELETE /api/personnel/{id})
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePersonnel(int id)
    {
        try
        {
            var employee = await _context.employees.FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null)
                return NotFound("Silinecek personel bulunamadı.");

            _context.employees.Remove(employee);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Personel silinemedi.", Error = ex.Message });
        }
    }

    // 6. İZİN VE MAAŞ KESİNTİSİ SİMÜLE ET (POST /api/personnel/calculate)
    [HttpPost("calculate")]
    public IActionResult CalculateDeduction([FromQuery] decimal monthlySalary, [FromQuery] LeaveType leaveType, [FromQuery] decimal leaveDays)
    {
        try
        {
            var result = _calculationService.CalculatePayroll(monthlySalary, leaveType, leaveDays);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Hesaplama simülasyonu başarısız.", Error = ex.Message });
        }
    }

    // 7. TÜM ONAYLI İZİN KAYITLARINI GETİR (GET /api/personnel/leaves)
    [HttpGet("leaves")]
    public async Task<IActionResult> GetAllLeaves()
    {
        try
        {
            var query = _context.leaveRecords
                .Include(l => l.employee)
                .Where(l => l.Status == LeaveStatus.Approved);

            if (!IsAdmin)
                query = query.Where(l => l.employeeId == CurrentEmployeeId);

            var leaves = await query
                .Select(l => new
                {
                    l.Id,
                    l.employeeId,
                    EmployeeName = l.employee != null ? l.employee.Name : "Bilinmiyor",
                    BaseSalary = l.employee != null ? l.employee.MonthlySalary : 0,
                    l.LeaveType,
                    l.LeaveDays,
                    l.StartDate,
                    l.EndDate,
                    DeductionAmount = l.CalculatedDeducation,
                    FinalSalary = l.FinalSalary,
                    l.Note,
                    Status = (int)l.Status
                })
                .ToListAsync();

            return Ok(leaves);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "İzin kayıtları alınamadı.", Error = ex.Message });
        }
    }

    // 8. DOĞRUDAN İZİN EKLE (POST /api/personnel/add-leave)
    [HttpPost("add-leave")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddLeaveRecord([FromBody] LeaveRecord leaveRecord)
    {
        try
        {
            var employee = await _context.employees.FirstOrDefaultAsync(e => e.Id == leaveRecord.employeeId);
            if (employee == null)
                return NotFound("İzin verilecek personel bulunamadı.");

            var calcResult = _calculationService.CalculatePayroll(employee.MonthlySalary, leaveRecord.LeaveType, leaveRecord.LeaveDays);

            leaveRecord.CompanyId = CompanyId;
            leaveRecord.CalculatedDeducation = calcResult.DeductionAmount;
            leaveRecord.FinalSalary = calcResult.FinalNetSalary;
            leaveRecord.Status = LeaveStatus.Approved;

            employee.WorkStatus = (WorkStatusType)3;

            await _context.leaveRecords.AddAsync(leaveRecord);
            await _context.SaveChangesAsync();

            return Ok(leaveRecord);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "İzin kaydı eklenemedi.", Error = ex.Message });
        }
    }

    // 9. İZİN TALEBİ GÖNDERME (POST /api/personnel/request-leave)
    [HttpPost("request-leave")]
    public async Task<IActionResult> RequestLeave([FromBody] LeaveRecord leaveRecord)
    {
        try
        {
            if (!IsAdmin)
                leaveRecord.employeeId = CurrentEmployeeId;

            var employee = await _context.employees.FirstOrDefaultAsync(e => e.Id == leaveRecord.employeeId);
            if (employee == null)
                return NotFound(new { Message = "İzin isteyecek personel bulunamadı." });

            if (leaveRecord.EndDate < leaveRecord.StartDate)
                return BadRequest(new { Message = "İzin bitiş tarihi, başlangıç tarihinden önce olamaz." });

            decimal requestedDays = leaveRecord.LeaveDays > 0 ? leaveRecord.LeaveDays : ((leaveRecord.EndDate.Date - leaveRecord.StartDate.Date).Days + 1);
            leaveRecord.LeaveDays = requestedDays;

            if (leaveRecord.LeaveType == LeaveType.YillikIzin)
            {
                int annualAllowance = employee.ExperienceYears >= 5 ? 20 : 14;

                var totalUsedDays = await _context.leaveRecords
                    .Where(l => l.employeeId == employee.Id && l.LeaveType == LeaveType.YillikIzin && l.Status == LeaveStatus.Approved)
                    .SumAsync(l => l.LeaveDays);

                decimal remainingDays = annualAllowance - totalUsedDays;

                if (requestedDays > remainingDays)
                {
                    return BadRequest(new { Message = $"❌ İzin talebi reddedildi! Yıllık izin hakkınız yetersiz. Kalan izin hakkınız: {remainingDays} gün." });
                }
            }

            var totalDeptStaff = await _context.employees.CountAsync(e => e.Department == employee.Department);

            var overlappingLeavesCount = await _context.leaveRecords
                .Include(l => l.employee)
                .Where(l => l.employee != null &&
                            l.employee.Department == employee.Department &&
                            l.Status == LeaveStatus.Approved &&
                            l.StartDate.Date <= leaveRecord.EndDate.Date &&
                            l.EndDate.Date >= leaveRecord.StartDate.Date)
                .Select(l => l.employeeId)
                .Distinct()
                .CountAsync();

            int remainingActiveStaff = totalDeptStaff - overlappingLeavesCount - 1;

            if (remainingActiveStaff < 2 && totalDeptStaff >= 3)
            {
                return BadRequest(new { Message = $"❌ İzin talebi reddedildi! '{employee.Department}' departmanında iş devamlılığı için en az 2 aktif personel bulunmalıdır." });
            }

            leaveRecord.CompanyId = CompanyId;
            leaveRecord.Status = LeaveStatus.Pending;
            leaveRecord.CalculatedDeducation = 0;
            leaveRecord.FinalSalary = employee.MonthlySalary;

            await _context.leaveRecords.AddAsync(leaveRecord);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = $"⏳ İzin talebiniz ({requestedDays} gün) başarıyla oluşturuldu ve Yönetici onayına gönderildi!",
                Record = leaveRecord
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "İzin talebi oluşturulamadı.", Error = ex.Message });
        }
    }

    // 10. BEKLEYEN İZİN TALEPLERİ (GET /api/personnel/pending-leaves)
    [HttpGet("pending-leaves")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPendingLeaves()
    {
        try
        {
            var pendingLeaves = await _context.leaveRecords
                .Include(l => l.employee)
                .Where(l => l.Status == LeaveStatus.Pending)
                .Select(l => new
                {
                    l.Id,
                    l.employeeId,
                    EmployeeName = l.employee != null ? l.employee.Name : "Bilinmiyor",
                    Department = l.employee != null ? l.employee.Department : "Genel",
                    BaseSalary = l.employee != null ? l.employee.MonthlySalary : 0,
                    l.LeaveType,
                    l.LeaveDays,
                    l.StartDate,
                    l.EndDate,
                    l.Note,
                    Status = (int)l.Status
                })
                .ToListAsync();

            return Ok(pendingLeaves);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Bekleyen izin talepleri alınamadı.", Error = ex.Message });
        }
    }

    // 11. YÖNETİCİ İZİN ONAYLAMA (POST /api/personnel/approve-leave/{id})
    [HttpPost("approve-leave/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveLeave(int id)
    {
        try
        {
            var leaveRecord = await _context.leaveRecords.Include(l => l.employee).FirstOrDefaultAsync(l => l.Id == id);
            if (leaveRecord == null || leaveRecord.employee == null) return NotFound("İzin kaydı bulunamadı.");

            var calcResult = _calculationService.CalculatePayroll(leaveRecord.employee.MonthlySalary, leaveRecord.LeaveType, leaveRecord.LeaveDays);

            leaveRecord.CalculatedDeducation = calcResult.DeductionAmount;
            leaveRecord.FinalSalary = calcResult.FinalNetSalary;
            leaveRecord.Status = LeaveStatus.Approved;
            leaveRecord.employee.WorkStatus = (WorkStatusType)3;

            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = $"✅ '{leaveRecord.employee.Name}' izin talebi onaylandı!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "İzin talebi onaylanamadı.", Error = ex.Message });
        }
    }

    // 12. YÖNETİCİ İZİN REDDETME (POST /api/personnel/reject-leave/{id})
    [HttpPost("reject-leave/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectLeave(int id)
    {
        try
        {
            var leaveRecord = await _context.leaveRecords.FirstOrDefaultAsync(l => l.Id == id);
            if (leaveRecord == null) return NotFound("Reddedilecek izin kaydı bulunamadı.");

            leaveRecord.Status = LeaveStatus.Rejected;
            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = $"🔴 #{id} numaralı izin talebi reddedildi." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "İzin talebi reddedilemedi.", Error = ex.Message });
        }
    }

    // 13. BİLDİRİM NOTU GÖNDER (POST /api/personnel/send-notification)
    [HttpPost("send-notification")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SendLeaveNotification([FromBody] LeaveNotificationRequest request)
    {
        try
        {
            var leaveRecord = await _context.leaveRecords.Include(l => l.employee).FirstOrDefaultAsync(l => l.Id == request.LeaveRecordId);
            if (leaveRecord == null) return NotFound("İzin kaydı bulunamadı.");

            return Ok(new
            {
                Success = true,
                Message = $"📢 Bildirim notu '{leaveRecord.employee?.Name}' için gönderildi!",
                Details = new { Recipient = leaveRecord.employee?.Name, Note = request.MessageNote }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Bildirim notu oluşturulamadı.", Error = ex.Message });
        }
    }

    // 14. TURNİKE RFID KART OKUTMA (POST /api/personnel/swipe-card)
    [HttpPost("swipe-card")]
    public async Task<IActionResult> SwipeCard([FromBody] CardSwipeRequest request)
    {
        try
        {
            var employeeId = IsAdmin ? request.EmployeeId : CurrentEmployeeId;
            var employee = await _context.employees.FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null) return NotFound("Kart okutulan personel bulunamadı.");

            string eventType = employee.WorkStatus != (WorkStatusType)1 ? "Giriş Yapıldı 🟢" : "Çıkış Yapıldı 🔴";
            employee.WorkStatus = employee.WorkStatus != (WorkStatusType)1 ? (WorkStatusType)1 : (WorkStatusType)4;

            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = $"💳 {employee.Name} - {eventType}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Kart okutma başarısız.", Error = ex.Message });
        }
    }

    // 15. EXCEL / CSV PERSONEL YÜKLE (POST /api/personnel/import-csv)
    [HttpPost("import-csv")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ImportCsv(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0) return BadRequest("Geçersiz dosya.");
            var newEmployees = new List<Employee>();

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                await reader.ReadLineAsync();
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    char sep = line.Contains(';') ? ';' : ',';
                    var p = line.Split(sep);
                    if (p.Length < 8) continue;

                    var newEmployee = new Employee
                    {
                        CompanyId = CompanyId,
                        Name = p[0].Trim(),
                        Department = p[1].Trim(),
                        ExperienceYears = int.TryParse(p[2].Trim(), out int exp) ? exp : 0,
                        EducationLevel = p[3].Trim(),
                        Age = int.TryParse(p[4].Trim(), out int age) ? age : 25,
                        Gender = p[5].Trim(),
                        MonthlySalary = decimal.TryParse(p[6].Trim(), out decimal sal) ? sal : 40000,
                        WorkStatus = (WorkStatusType)(int.TryParse(p[7].Trim(), out int st) ? st : 1)
                    };
                    // Geçici şifre; kayıttan sonra gerçek ID belli olunca personelin kendi ID'sine güncellenecek.
                    newEmployee.PasswordHash = _passwordHasher.HashPassword(newEmployee, "temp");
                    newEmployees.Add(newEmployee);
                }
            }

            await _context.employees.AddRangeAsync(newEmployees);
            await _context.SaveChangesAsync();

            // Varsayılan şifre = personelin kendi ID'si
            foreach (var emp in newEmployees)
            {
                emp.PasswordHash = _passwordHasher.HashPassword(emp, emp.Id.ToString());
            }
            await _context.SaveChangesAsync();

            return Ok(new { Success = true, Message = $"✅ {newEmployees.Count} personel eklendi. Varsayılan şifreleri kendi Personel ID'leridir (ilk girişte değiştirmelerini isteyin)." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "CSV yüklenemedi.", Error = ex.Message });
        }
    }

    // 16. EXCEL İZİN DÖKÜMÜ (GET /api/personnel/export-leaves-csv)
    [HttpGet("export-leaves-csv")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportLeavesCsv()
    {
        try
        {
            var leaves = await _context.leaveRecords.Include(l => l.employee).Where(l => l.Status == LeaveStatus.Approved).ToListAsync();
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Kayıt ID,Personel Adı,Departman,İzin Türü,Başlangıç,Bitiş,İzin Günü,Taban Maaş,Kesinti,Net Maaş,Açıklama");

            foreach (var l in leaves)
            {
                builder.AppendLine($"{l.Id},{l.employee?.Name},{l.employee?.Department},{l.LeaveType},{l.StartDate:dd.MM.yyyy},{l.EndDate:dd.MM.yyyy},{l.LeaveDays},{l.employee?.MonthlySalary},{l.CalculatedDeducation},{l.FinalSalary},{l.Note}");
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", "Izin_Dokumu.csv");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "İzin dökümü indirilemedi.", Error = ex.Message });
        }
    }

    // 17. AVANS/MASRAF TALEBİ OLUŞTUR (POST /api/personnel/request-advance-expense)
    [HttpPost("request-advance-expense")]
    public async Task<IActionResult> RequestAdvanceExpense([FromBody] AdvanceExpenseRecord record)
    {
        try
        {
            if (!IsAdmin)
                record.employeeId = CurrentEmployeeId;

            var employee = await _context.employees.FirstOrDefaultAsync(e => e.Id == record.employeeId);
            if (employee == null) return NotFound("Personel bulunamadı.");

            record.CompanyId = CompanyId;
            record.Status = RequestStatusType.Pending;
            record.RequestDate = DateTime.Now;

            await _context.advanceExpenseRecords.AddAsync(record);
            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = "⏳ Talep oluşturuldu!", Record = record });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Talep oluşturulamadı.", Error = ex.Message });
        }
    }

    // 18. BEKLEYEN AVANSLAR (GET /api/personnel/pending-advances)
    [HttpGet("pending-advances")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPendingAdvances()
    {
        try
        {
            var pending = await _context.advanceExpenseRecords.Include(a => a.employee).Where(a => a.Status == RequestStatusType.Pending).ToListAsync();
            return Ok(pending);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Bekleyen avanslar alınamadı.", Error = ex.Message });
        }
    }

    // 19. AVANS ONAYLA (POST /api/personnel/approve-advance/{id})
    [HttpPost("approve-advance/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveAdvanceExpense(int id)
    {
        try
        {
            var record = await _context.advanceExpenseRecords.FirstOrDefaultAsync(a => a.Id == id);
            if (record == null) return NotFound("Talep bulunamadı.");

            record.Status = RequestStatusType.Approved;
            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = "✅ Talep onaylandı!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Talep onaylanamadı.", Error = ex.Message });
        }
    }

    // 20. AVANS REDDET (POST /api/personnel/reject-advance/{id})
    [HttpPost("reject-advance/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectAdvanceExpense(int id)
    {
        try
        {
            var record = await _context.advanceExpenseRecords.FirstOrDefaultAsync(a => a.Id == id);
            if (record == null) return NotFound("Talep bulunamadı.");

            record.Status = RequestStatusType.Rejected;
            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = "🔴 Talep reddedildi." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Talep reddedilemedi.", Error = ex.Message });
        }
    }

    // 21. DUYURULARI GETİR (GET /api/personnel/announcements)
    [HttpGet("announcements")]
    public async Task<IActionResult> GetAnnouncements()
    {
        try
        {
            var list = await _context.announcements.Where(a => a.IsActive).OrderByDescending(a => a.CreatedAt).ToListAsync();
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Duyurular alınamadı.", Error = ex.Message });
        }
    }

    // 22. DUYURU YAYINLA (POST /api/personnel/announcements)
    [HttpPost("announcements")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAnnouncement([FromBody] Announcement announcement)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(announcement.Title) || string.IsNullOrWhiteSpace(announcement.Content))
                return BadRequest(new { Message = "Duyuru başlığı ve içeriği boş olamaz." });

            announcement.CompanyId = CompanyId;
            announcement.CreatedAt = DateTime.Now;
            announcement.IsActive = true;

            await _context.announcements.AddAsync(announcement);
            await _context.SaveChangesAsync();

            return Ok(new { Success = true, Message = $"📢 '{announcement.Title}' duyurusu yayınlandı!", Announcement = announcement });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Duyuru yayınlanamadı.", Error = ex.Message });
        }
    }

    // 23. DUYURU SİL (DELETE /api/personnel/announcements/{id})
    [HttpDelete("announcements/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAnnouncement(int id)
    {
        try
        {
            var item = await _context.announcements.FirstOrDefaultAsync(a => a.Id == id);
            if (item == null) return NotFound("Duyuru bulunamadı.");

            _context.announcements.Remove(item);
            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = "🗑️ Duyuru silindi." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Duyuru silinemedi.", Error = ex.Message });
        }
    }

    // 24. RESMİ BORDRO ZARFI VERİ KAPISI (GET /api/personnel/payslip/{leaveId})
    [HttpGet("payslip/{leaveId}")]
    public async Task<IActionResult> GetPayslipData(int leaveId)
    {
        try
        {
            var leave = await _context.leaveRecords
                .Include(l => l.employee)
                .FirstOrDefaultAsync(l => l.Id == leaveId);

            if (leave == null || leave.employee == null)
                return NotFound(new { Message = "Bordro çıkarılacak izin kaydı bulunamadı." });

            if (!IsAdmin && leave.employeeId != CurrentEmployeeId)
                return Forbid();

            var emp = leave.employee;

            var advanceDeductions = await _context.advanceExpenseRecords
                .Where(a => a.employeeId == emp.Id && a.Type == RequestCategoryType.Advance && a.Status == RequestStatusType.Approved)
                .SumAsync(a => a.Amount);

            decimal grossSalary = emp.MonthlySalary;
            decimal leaveDeduction = leave.CalculatedDeducation;
            decimal netSalary = grossSalary - leaveDeduction - advanceDeductions;

            return Ok(new
            {
                Success = true,
                PayslipNo = $"BRD-{DateTime.Now:yyyyMM}-{leave.Id:D4}",
                IssueDate = DateTime.Now.ToString("dd.MM.yyyy"),
                Employee = new
                {
                    Id = emp.Id,
                    Name = emp.Name,
                    Department = emp.Department,
                    ExperienceYears = emp.ExperienceYears
                },
                PayrollDetails = new
                {
                    GrossSalary = grossSalary,
                    DailyWage = Math.Round(grossSalary / 30m, 2),
                    LeaveDays = leave.LeaveDays,
                    LeaveType = leave.LeaveType.ToString(),
                    LeaveDeduction = leaveDeduction,
                    AdvanceDeduction = advanceDeductions,
                    NetPayableSalary = netSalary > 0 ? netSalary : 0
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Bordro verisi hazırlanamadı.", Error = ex.Message });
        }
    }
}
