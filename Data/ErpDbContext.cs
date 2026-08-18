using ErpPersonelLeaveSystem.models;
using Microsoft.EntityFrameworkCore;

namespace ErpPersonelLeaveSystem.Data;

//EFC nin ana veri tabanı yönetim sınıfı 
public class ErpDbContext : DbContext //dbcontext ten yetenekler miras alır (kodu kendi okuyabileceği şekilde derleme yetenekleri)
{
    public ErpDbContext()
    { 
    }
    /* biz ilk başta kodumuzu db direkt olarak ayarlı ve kurallı bir şekilde çalıştırılacak şekilde kodlamıştık,
     * ve biz db yi dotnet  ef komutuyla çalıştırmaya çalıştık bu komutta ilk olarak kuralsız, komutsuz, ayarsız
     * birtablo oluşturup onun üzerine eklemeler yapmak ister. dolayısıyla bizde ana tablodan önce kuralsızbir
     * tablo oluşturduk*/
    
    //Program.cs den gönderilen veritabanı konfigürasyon paketini ( options ) miras alınan ,üst DbContext sınfına aktaran kurucu metottur.
    public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options)
    {

    }
    
    // SQL SERVER DA OLUŞACAK EMPLOYEES tablomuz
    public DbSet<Employee> employees { get; set; }

    //SQL Server da oluşacak 'LeaveRecord' Tablomuz 
    public DbSet<LeaveRecord> leaveRecords { get; set; }

    public DbSet<AdvanceExpenseRecord> advanceExpenseRecords { get; set; } //sql server da oluşacak avans gider tablomuz 

    public DbSet<Announcement> announcements { get; set; } 





}


