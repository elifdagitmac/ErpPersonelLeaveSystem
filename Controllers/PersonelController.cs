using ErpPersonelLeaveSystem.Data;
using ErpPersonelLeaveSystem.models;
using ErpPersonelLeaveSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpPersonelLeaveSystem.Controllers;

public class CardSwipeRequest
{
    public int EmployeeId { get; set; }
    public string CardUid { get; set; } = string.Empty;
}

[ApiController]
[Route("api/personnel")]
public class PersonnelController : ControllerBase
{
    private readonly ErpDbContext _context;
    private readonly ILeaveCalculationService _calculationService;

    public PersonnelController(ErpDbContext context, ILeaveCalculationService calculationService)
    {
        _context = context;
        _calculationService = calculationService;
    }

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
            var employee = await _context.employees.FindAsync(id);
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
    public async Task<IActionResult> CreatePersonnel([FromBody] Employee employee)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _context.employees.AddAsync(employee);
            await _context.SaveChangesAsync();

            return Ok(employee);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Personel eklenemedi.", Error = ex.Message });
        }
    }

    // 4. PERSONEL BİLGİLERİNİ GÜNCELLE (PUT /api/personnel/{id})
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePersonnel(int id, [FromBody] Employee updatedEmployee)
    {
        try
        {
            var existingEmployee = await _context.employees.FindAsync(id);
            if (existingEmployee == null)
                return NotFound("Güncellenecek personel bulunamadı.");

            existingEmployee.Name = updatedEmployee.Name;
            existingEmployee.Department = updatedEmployee.Department;
            existingEmployee.MonthlySalary = updatedEmployee.MonthlySalary;
            existingEmployee.ExperienceYears = updatedEmployee.ExperienceYears;
            existingEmployee.Age = updatedEmployee.Age;
            existingEmployee.WorkStatus = updatedEmployee.WorkStatus;

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
    public async Task<IActionResult> DeletePersonnel(int id)
    {
        try
        {
            var employee = await _context.employees.FindAsync(id);
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
    public IActionResult CalculateDeduction([FromQuery] decimal monthlySalary, [FromQuery] LeaveType leaveType, [FromQuery] int leaveDays)
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
            var leaves = await _context.leaveRecords
                .Include(l => l.employee)
                .Where(l => l.Status == LeaveStatus.Approved)
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

    // 8. DOĞRUDAN İZİN EKLE (ESKİ ONAY YÖNTEMİ) (POST /api/personnel/add-leave)
    [HttpPost("add-leave")]
    public async Task<IActionResult> AddLeaveRecord([FromBody] LeaveRecord leaveRecord)
    {
        try
        {
            var employee = await _context.employees.FindAsync(leaveRecord.employeeId);
            if (employee == null)
                return NotFound("İzin verilecek personel bulunamadı.");

            var calcResult = _calculationService.CalculatePayroll(employee.MonthlySalary, leaveRecord.LeaveType, leaveRecord.LeaveDays);

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

    // 9. AKILLI ÇALIŞAN İZİN TALEBİ GÖNDERME KAPISI (POST /api/personnel/request-leave)
    [HttpPost("request-leave")]
    public async Task<IActionResult> RequestLeave([FromBody] LeaveRecord leaveRecord)
    {
        try
        {
            var employee = await _context.employees.FindAsync(leaveRecord.employeeId);
            if (employee == null)
                return NotFound(new { Message = "İzin isteyecek personel bulunamadı." });

            if (leaveRecord.EndDate <= leaveRecord.StartDate)
                return BadRequest(new { Message = "İzin bitiş tarihi, başlangıç tarihinden sonra olmalıdır." });

            int requestedDays = (leaveRecord.EndDate.Date - leaveRecord.StartDate.Date).Days + 1;
            leaveRecord.LeaveDays = requestedDays;

            // 🛡️ 1. ALTIN KURAL: YILLIK İZİN HAKKI KONTROLÜ
            if (leaveRecord.LeaveType == LeaveType.YillikIzin)
            {
                int annualAllowance = employee.ExperienceYears >= 5 ? 20 : 14;

                var totalUsedDays = await _context.leaveRecords
                    .Where(l => l.employeeId == employee.Id && l.LeaveType == LeaveType.YillikIzin && l.Status == LeaveStatus.Approved)
                    .SumAsync(l => l.LeaveDays);

                int remainingDays = annualAllowance - totalUsedDays;

                if (requestedDays > remainingDays)
                {
                    return BadRequest(new { Message = $"❌ İzin talebi reddedildi! Yıllık izin hakkınız yetersiz. Kalan izin hakkınız: {remainingDays} gün. Talep edilen: {requestedDays} gün." });
                }
            }

            // 🛡️ 2. ALTIN KURAL: DEPARTMAN MİNİMUM İŞ DEVAMLILIĞI (EN AZ 2 KİŞİ KALMALI!)
            var totalDeptStaff = await _context.employees
                .CountAsync(e => e.Department == employee.Department);

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
                return BadRequest(new { Message = $"❌ İzin talebi reddedildi! '{employee.Department}' departmanında iş devamlılığı için en az 2 aktif personel bulunmalıdır. O tarihlerde aktif kalacak personel sayısı: {remainingActiveStaff + 1}." });
            }

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

    // 10. BEKLEYEN İZİN TALEPLERİNİ GETİR (GET /api/personnel/pending-leaves)
    [HttpGet("pending-leaves")]
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

    // 11. YÖNETİCİ İZİN TALEBİ ONAYLAMA KAPISI (POST /api/personnel/approve-leave/{id})
    [HttpPost("approve-leave/{id}")]
    public async Task<IActionResult> ApproveLeave(int id)
    {
        try
        {
            var leaveRecord = await _context.leaveRecords
                .Include(l => l.employee)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (leaveRecord == null)
                return NotFound("Onaylanacak izin kaydı bulunamadı.");

            if (leaveRecord.employee == null)
                return NotFound("İzin kaydına bağlı personel bulunamadı.");

            var calcResult = _calculationService.CalculatePayroll(leaveRecord.employee.MonthlySalary, leaveRecord.LeaveType, leaveRecord.LeaveDays);

            leaveRecord.CalculatedDeducation = calcResult.DeductionAmount;
            leaveRecord.FinalSalary = calcResult.FinalNetSalary;
            leaveRecord.Status = LeaveStatus.Approved;

            leaveRecord.employee.WorkStatus = (WorkStatusType)3;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = $"✅ '{leaveRecord.employee.Name}' adlı personelin izin talebi onaylandı ve maaş kesintisi işlendi!"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "İzin talebi onaylanamadı.", Error = ex.Message });
        }
    }

    // 12. YÖNETİCİ İZİN TALEBİ REDDETME KAPISI (POST /api/personnel/reject-leave/{id})
    [HttpPost("reject-leave/{id}")]
    public async Task<IActionResult> RejectLeave(int id)
    {
        try
        {
            var leaveRecord = await _context.leaveRecords
                .Include(l => l.employee)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (leaveRecord == null)
                return NotFound("Reddedilecek izin kaydı bulunamadı.");

            leaveRecord.Status = LeaveStatus.Rejected;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = $"🔴 #{id} numaralı izin talebi reddedildi."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "İzin talebi reddedilemedi.", Error = ex.Message });
        }
    }

    // 13. PERSONELE İZİN BİLDİRİM NOTU GÖNDER (POST /api/personnel/send-notification)
    [HttpPost("send-notification")]
    public async Task<IActionResult> SendLeaveNotification([FromBody] LeaveNotificationRequest request)
    {
        try
        {
            var leaveRecord = await _context.leaveRecords
                .Include(l => l.employee)
                .FirstOrDefaultAsync(l => l.Id == request.LeaveRecordId);

            if (leaveRecord == null)
                return NotFound("İzin kaydı bulunamadı.");

            var employeeName = leaveRecord.employee != null ? leaveRecord.employee.Name : "Personel";

            return Ok(new
            {
                Success = true,
                Message = $"📢 Sistem içi bildirim notu '{employeeName}' için başarıyla oluşturuldu!",
                Details = new
                {
                    Recipient = employeeName,
                    LeaveRecordId = leaveRecord.Id,
                    Note = request.MessageNote,
                    CreatedAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Bildirim notu oluşturulamadı.", Error = ex.Message });
        }
    }

    // 14. TURNİKEDEN RFID KART OKUTMA KAPISI (POST /api/personnel/swipe-card)
    [HttpPost("swipe-card")]
    public async Task<IActionResult> SwipeCard([FromBody] CardSwipeRequest request)
    {
        try
        {
            var employee = await _context.employees.FindAsync(request.EmployeeId);
            if (employee == null)
                return NotFound("Kart okutulan personel bulunamadı.");

            string eventType;
            if (employee.WorkStatus != (WorkStatusType)1)
            {
                employee.WorkStatus = (WorkStatusType)1;
                eventType = "Giriş Yapıldı 🟢";
            }
            else
            {
                employee.WorkStatus = (WorkStatusType)4;
                eventType = "Çıkış Yapıldı 🔴";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = $"💳 {employee.Name} - {eventType}",
                EmployeeId = employee.Id,
                NewStatus = (int)employee.WorkStatus,
                StatusLabel = employee.WorkStatus.ToString(),
                Timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Kart okutma işlemi başarısız.", Error = ex.Message });
        }
    }

    // 15. KURUMSAL KULLANICI GİRİŞİ (POST /api/personnel/login)
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (!await _context.employees.AnyAsync())
            {
                var defaultAdmin = new Employee
                {
                    Name = "test testtest",
                    Department = "IT",
                    ExperienceYears = 10,
                    EducationLevel = "Lisans",
                    Age = 35,
                    Gender = "Erkek",
                    MonthlySalary = 100000,
                    WorkStatus = (WorkStatusType)1
                };
                await _context.employees.AddAsync(defaultAdmin);
                await _context.SaveChangesAsync();
            }

            Employee? employee = null;
            if (request.EmployeeId > 0)
            {
                employee = await _context.employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId);
            }

            if (employee == null && !string.IsNullOrWhiteSpace(request.Name))
            {
                var searchName = request.Name.Trim().ToLower();
                employee = await _context.employees
                    .FirstOrDefaultAsync(e => e.Name.ToLower().Contains(searchName));
            }

            if (employee == null)
            {
                employee = await _context.employees.FirstOrDefaultAsync();
            }

            if (employee == null)
                return NotFound(new { Message = "Sistemde kayıtlı personel bulunamadı." });

            var adminDepartments = new[] { "yönetim", "yonetim", "ik", "it", "yönetici", "yonetici", "ik & yönetim" };
            string userRole = (employee.Id == 1 || adminDepartments.Contains(employee.Department.Trim().ToLower()))
                ? "Admin"
                : "Employee";

            return Ok(new
            {
                Success = true,
                Message = $"🔑 Hoş geldiniz, {employee.Name}!",
                Employee = new
                {
                    Id = employee.Id,
                    Name = employee.Name,
                    Department = employee.Department,
                    Role = userRole,
                    WorkStatus = (int)employee.WorkStatus
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Giriş işlemi başarısız.", Error = ex.Message });
        }
    }

    // 16. SAHTE TEST VERİSETİNİ VERİTABANINA YÜKLE (POST /api/personnel/seed-data)
    [HttpPost("seed-data")]
    public async Task<IActionResult> SeedTestData()
    {
        try
        {
            if (await _context.employees.AnyAsync(e => e.Name == "Ahmet Yılmaz (CEO)"))
                return BadRequest("Sahte test verileri zaten veritabanında mevcut.");

            var testEmployees = new List<Employee>
            {
                new Employee { Name = "Ahmet Yılmaz (CEO)", Department = "Yönetim", ExperienceYears = 15, EducationLevel = "Doktora", Age = 45, Gender = "Erkek", MonthlySalary = 120000, WorkStatus = (WorkStatusType)1 },
                new Employee { Name = "Ayşe Kaya (İK Müdürü)", Department = "İK", ExperienceYears = 10, EducationLevel = "Yüksek Lisans", Age = 38, Gender = "Kadın", MonthlySalary = 85000, WorkStatus = (WorkStatusType)1 },
                new Employee { Name = "Mehmet Demir (IT Lideri)", Department = "IT", ExperienceYears = 8, EducationLevel = "Lisans", Age = 32, Gender = "Erkek", MonthlySalary = 75000, WorkStatus = (WorkStatusType)2 },
                new Employee { Name = "Zeynep Arslan (Yazılımcı)", Department = "Yazılım", ExperienceYears = 3, EducationLevel = "Lisans", Age = 26, Gender = "Kadın", MonthlySalary = 48000, WorkStatus = (WorkStatusType)1 },
                new Employee { Name = "Can Öztürk (Pazarlama)", Department = "Pazarlama", ExperienceYears = 4, EducationLevel = "Lisans", Age = 28, Gender = "Erkek", MonthlySalary = 42000, WorkStatus = (WorkStatusType)4 }
            };

            await _context.employees.AddRangeAsync(testEmployees);
            await _context.SaveChangesAsync();

            return Ok(new { Success = true, Message = "🌱 5 adet zengin örnek test personeli veritabanına başarıyla yüklendi!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Test verileri yüklenemedi.", Error = ex.Message });
        }
    }

    // 17. YÖNETİCİ İZİN İPTAL ETME KAPISI (DELETE /api/personnel/cancel-leave/{id})
    [HttpDelete("cancel-leave/{id}")]
    public async Task<IActionResult> CancelLeave(int id)
    {
        try
        {
            var leaveRecord = await _context.leaveRecords
                .Include(l => l.employee)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (leaveRecord == null)
                return NotFound("İptal edilecek izin kaydı bulunamadı.");

            var employeeName = leaveRecord.employee != null ? leaveRecord.employee.Name : "Personel";

            _context.leaveRecords.Remove(leaveRecord);

            if (leaveRecord.employee != null && leaveRecord.employee.WorkStatus == (WorkStatusType)3)
            {
                leaveRecord.employee.WorkStatus = (WorkStatusType)1;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = $"🗑️ '{employeeName}' adlı personelin #{id} numaralı izin kaydı başarıyla iptal edildi ve maaş kesintisi kaldırıldı!"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "İzin kaydı iptal edilemedi.", Error = ex.Message });
        }
    }

    // 18. EXCEL / CSV DOSYASINDAN TOPLU PERSONEL YÜKLE (POST /api/personnel/import-csv)
    [HttpPost("import-csv")]
    public async Task<IActionResult> ImportCsv(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "Lütfen geçerli bir CSV veya Excel dosyası seçiniz." });

            var newEmployees = new List<Employee>();

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                string? headerLine = await reader.ReadLineAsync(); // Header satırını atla
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Hem virgül (,) hem noktalı virgül (;) otomatik algılama
                    char separator = line.Contains(';') ? ';' : ',';
                    var parts = line.Split(separator);

                    if (parts.Length < 8) continue;

                    var emp = new Employee
                    {
                        Name = parts[0].Trim(),
                        Department = parts[1].Trim(),
                        ExperienceYears = int.TryParse(parts[2].Trim(), out int exp) ? exp : 0,
                        EducationLevel = string.IsNullOrWhiteSpace(parts[3].Trim()) ? "Lisans" : parts[3].Trim(),
                        Age = int.TryParse(parts[4].Trim(), out int age) ? age : 25,
                        Gender = string.IsNullOrWhiteSpace(parts[5].Trim()) ? "Belirtilmedi" : parts[5].Trim(),
                        MonthlySalary = decimal.TryParse(parts[6].Trim(), out decimal sal) ? sal : 40000,
                        WorkStatus = (WorkStatusType)(int.TryParse(parts[7].Trim(), out int status) ? status : 1)
                    };

                    newEmployees.Add(emp);
                }
            }

            if (newEmployees.Count == 0)
                return BadRequest(new { Message = "Dosyadan okunabilir personel verisi çıkarılamadı." });

            await _context.employees.AddRangeAsync(newEmployees);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = $"✅ {newEmployees.Count} adet personel başarıyla veritabanına aktarıldı ve sisteme işlendi!",
                TotalImported = newEmployees.Count
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "CSV yükleme işlemi başarısız.", Error = ex.Message });
        }
    }
}