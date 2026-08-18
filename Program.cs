using ErpPersonelLeaveSystem.Data;
using ErpPersonelLeaveSystem.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SQLite Veritabanı Bağlantısı
builder.Services.AddDbContext<ErpDbContext>(options =>
    options.UseSqlite("Data Source=erp.db"));

// İzin Hesaplama Servisi
builder.Services.AddScoped<ILeaveCalculationService, LeaveCalculationService>();

var app = builder.Build();

// Veritabanını ve Eksik Tabloları Otomatik Oluştur
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
    db.Database.EnsureCreated();

    // 🛡️ EKSİK TABLOLARI OTOMATİK OLUŞTURMA KURALI
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS announcements (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL,
            Content TEXT NOT NULL,
            Category TEXT NOT NULL,
            Priority TEXT NOT NULL,
            PublisherName TEXT,
            CreatedAt TEXT NOT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1
        );
    ");

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS advanceExpenseRecords (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            employeeId INTEGER NOT NULL,
            Type INTEGER NOT NULL,
            Amount TEXT NOT NULL,
            Description TEXT NOT NULL,
            RequestDate TEXT NOT NULL,
            Status INTEGER NOT NULL DEFAULT 1
        );
    ");
}

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();