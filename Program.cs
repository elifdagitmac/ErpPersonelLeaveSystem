//VERİTABANI, MAAŞ HESAPLAMA SERVİSİ VE TEST EKRANI ALTYAPISINI BİRLEŞTİRİP PROJEYİ AYAĞA KALDIRIR.



using ErpPersonelLeaveSystem.Data;
using ErpPersonelLeaveSystem.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args); // komut satırından gelen başlatma parametreleri ile ana yapılandırıcı nesneyi oluştur. bu nesnenin ismi builder ve nesnenin tipini compiler var komutu sayesinde kendisi anlar.

//apı controller servislerini ve swagger test ekranını ekle 
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); //tarayıcıda açılacak olan swagger test ekranının altyapısını projeye yükler. 

// appsetingsjson dosyasındaki adresi alıp erpdbcotext e teslim 
builder.Services.AddDbContext<ErpDbContext>(options =>
     options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ınterface ile gerçek sınıfı birbirine bağlamak (dependency injection)
builder.Services.AddScoped<ILeaveCalculationService, LeaveCalculationService>();

var app = builder.Build(); //builder nesnesinin içerisindeki build fonksiyonunu çalıştırır ve app i kurar 

//swagger test arayüzü aktifleşir
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

}

app.UseHttpsRedirection(); //bir kullanıcı güvenli olmayan bit http adresiyle gelirse onu otoatik olarak güveli https adresine yönlendirir.
app.UseAuthorization(); //Gelen kullanıcının istediği işlemi yapmaya yetkisi var mı yok mu onu kontrol eder.
app.MapControllers();//Tarayıcıdan bir adres isteği geldiğinde sistem arka planda bu adresi projedeki doğru controller sınıfına otomatik yönlendirir.

app.Run();

/*AddScoped: Servisin hafızadaki yaşam süresini belirler, arayüzden gelen her yeni istekte hafızada sıfır bir LeaveCalculationService nesnesi oluşturur, işi bitince onu hafızadan siler.
*servis kodları hafızada bulunuyor zaten arayüzden her istek geldiğinde servis yeniden oluşturuluyor ve siliniyor*/

/* builder.Services.AddDbContext<ErpDbConext> ---- ErpDbContext sınıfını projenin servis havuzuna kaydeder. projenin diğer yerleri db ye erişmek istediğinde derleyici sınıfı otomatik olarak tanıyabilsin diye
 * options: ErpDbContext için ayar yapılandırması (hangi server motorunu kullanacağı, bağlanacağı adres) başlatan parametre
 */