using ErpPersonelLeaveSystem.models;
using Microsoft.EntityFrameworkCore;

namespace ErpPersonelLeaveSystem.Data;

//EFC nin ana veri tabanı yönetim sınıfı 
public class ErpDbContext : DbContext
{
    //veritabanı yapılandırma ayarlarnı alan kurucu method 
    public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options)
    {

    }

    // SQL SERVER DA OLUŞACAK EMPLOYEES tablomuz
    public DbSet<employee> employees { get; set; }

    //SQL Server da oluşacak 'LeaveRecord' Tablomuz 
    public DbSet<LeaveRecord> leaveRecords { get; set; }









}


