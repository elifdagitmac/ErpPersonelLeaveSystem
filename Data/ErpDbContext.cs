using ErpPersonelLeaveSystem.models;
using ErpPersonelLeaveSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace ErpPersonelLeaveSystem.Data;

//EFC nin ana veri tabanı yönetim sınıfı
public class ErpDbContext : DbContext //dbcontext ten yetenekler miras alır (kodu kendi okuyabileceği şekilde derleme yetenekleri)
{
    private readonly ICurrentTenantService? _tenantService;

    public ErpDbContext()
    {
    }

    //Program.cs den gönderilen veritabanı konfigürasyon paketini ( options ) miras alınan ,üst DbContext sınfına aktaran kurucu metottur.
    public ErpDbContext(DbContextOptions<ErpDbContext> options, ICurrentTenantService? tenantService = null) : base(options)
    {
        _tenantService = tenantService;
    }

    // Şirketler (Tenant) tablosu
    public DbSet<Company> companies { get; set; }

    // SQL SERVER DA OLUŞACAK EMPLOYEES tablomuz
    public DbSet<Employee> employees { get; set; }

    //SQL Server da oluşacak 'LeaveRecord' Tablomuz
    public DbSet<LeaveRecord> leaveRecords { get; set; }

    public DbSet<AdvanceExpenseRecord> advanceExpenseRecords { get; set; } //sql server da oluşacak avans gider tablomuz

    public DbSet<Announcement> announcements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>().HasIndex(c => c.CompanyCode).IsUnique();

        // Zorunlu Sorgu İzolasyonu (Multi-Tenant Query Filtering):
        // SuperAdmin oturumunda (CompanyId claim'i yok) hiçbir kayıt görünmez;
        // Company-scope endpoint'ler IgnoreQueryFilters kullanmadıkça bu filtre her sorguda otomatik uygulanır.
        modelBuilder.Entity<Employee>().HasQueryFilter(e => _tenantService != null && e.CompanyId == _tenantService.CompanyId);
        modelBuilder.Entity<LeaveRecord>().HasQueryFilter(l => _tenantService != null && l.CompanyId == _tenantService.CompanyId);
        modelBuilder.Entity<AdvanceExpenseRecord>().HasQueryFilter(a => _tenantService != null && a.CompanyId == _tenantService.CompanyId);
        modelBuilder.Entity<Announcement>().HasQueryFilter(a => _tenantService != null && a.CompanyId == _tenantService.CompanyId);
    }
}
