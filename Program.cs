using ErpPersonelLeaveSystem.Data;
using ErpPersonelLeaveSystem.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Controller ve Swagger Servislerini Ekle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Veritabanı Servisini (ErpDbContext) Kaydet
builder.Services.AddDbContext<ErpDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=erp.db"));

// 3. İzin ve Bordro Hesaplama Servisini Kaydet (DI)
builder.Services.AddScoped<ILeaveCalculationService, LeaveCalculationService>();

// 4. CORS İznini Tanımla (Tarayıcı İletişimi İçin)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// 5. ESKİ TABLOYU TEMİZLE VE YENİ SÜTUNLARLA BAŞTAN KUR (EnsureDeleted & EnsureCreated) 🛡️
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
    db.Database.EnsureDeleted(); // Eski sütunsuz veritabanı şemasını siler
    db.Database.EnsureCreated(); // StartDate, EndDate ve Status sütunlarıyla baştan kurar
}

// 6. HTTP Pipeline ve Statik Dosya Yapılandırması
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseStaticFiles(); // wwwroot klasörünü dışarı açar (index.html, app.js vb.)
app.UseRouting();
app.UseAuthorization();

app.MapControllers();

app.Run();