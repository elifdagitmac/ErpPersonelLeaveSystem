# ErpPersonnel | İzin ve Maaş Takip Sistemi

Küçük ve orta ölçekli işletmeler için geliştirilmiş, personel yönetimi, izin talebi/onay süreci, maaş kesinti simülasyonu, avans/masraf talepleri ve şirket içi duyuruları tek bir panelde toplayan bir **ERP (İnsan Kaynakları) modülü**.

Backend **ASP.NET Core Web API (.NET 10)** ve **Entity Framework Core** ile, frontend ise ek bir framework kullanmadan **vanilla HTML/CSS/JavaScript** ile geliştirilmiştir. Uygulama tek bir proje içinde çalışır: API ve statik arayüz aynı sunucudan servis edilir.

## İçindekiler

- [Özellikler](#özellikler)
- [Kullanılan Teknolojiler](#kullanılan-teknolojiler)
- [Proje Yapısı](#proje-yapısı)
- [Kurulum](#kurulum)
- [Çalıştırma](#çalıştırma)
- [Veritabanı](#veritabanı)
- [API Uç Noktaları](#api-uç-noktaları)
- [Roller ve Giriş Mantığı](#roller-ve-giriş-mantığı)
- [Bilinen Kısıtlar / Yapılacaklar](#bilinen-kısıtlar--yapılacaklar)
- [Lisans](#lisans)

## Özellikler

- **Personel Yönetimi:** Personel ekleme, güncelleme, silme ve listeleme; CSV/Excel içe aktarma ile toplu personel yükleme.
- **İzin Talep & Onay Süreci:** Personelin izin talebinde bulunması, yöneticinin talebi onaylaması/reddetmesi, bekleyen taleplerin listelenmesi.
- **Otomatik İzin Hakkı Kontrolü:** Kıdem yılına göre yıllık izin hakkı hesaplama (5 yıl ve üzeri için 20 gün, altı için 14 gün) ve kalan hak kontrolü.
- **Departman Devamlılık Kuralı:** Aynı departmanda çakışan izinler için asgari aktif personel sayısı kontrolü (iş sürekliliğini korumak amacıyla).
- **Maaş Kesinti Simülatörü:** İzin türüne göre (yıllık, ücretsiz, sağlık, mazeret) canlı maaş kesintisi hesaplama.
- **Avans / Masraf Talebi:** Personelin avans veya masraf/harcırah talebi oluşturması, yönetici tarafından onaylanması/reddedilmesi.
- **Resmi Bordro Çıktısı:** Onaylanan izin kaydına bağlı, avans kesintilerini de içeren bordro verisi üretimi.
- **Duyuru Panosu:** Şirket geneli duyuru yayınlama, listeleme ve silme (kategori/öncelik etiketli).
- **Departman Bazlı İzin Takvimi:** Onaylanmış izinlerin görsel takvim üzerinde takibi.
- **RFID Kart Okutma Simülasyonu:** Giriş/çıkış olaylarını simüle eden turnike kart okutma uç noktası.
- **CSV Dışa Aktarım:** Onaylı izin kayıtlarının CSV olarak indirilmesi.
- **Swagger / OpenAPI:** Geliştirme ortamında hazır API dokümantasyonu arayüzü.

## Kullanılan Teknolojiler

| Katman | Teknoloji |
|---|---|
| Backend | ASP.NET Core Web API (.NET 10) |
| ORM | Entity Framework Core 10 |
| Veritabanı | SQLite (varsayılan, `erp.db`) — SQL Server desteği de mevcut |
| Frontend | HTML5, CSS3, Vanilla JavaScript (framework yok) |
| API Dokümantasyonu | Swagger / Swashbuckle |

## Proje Yapısı

```
ErpPersonelLeaveSystem/
├── Controllers/
│   └── PersonelController.cs      # Tüm API uç noktaları (personel, izin, avans, duyuru, bordro)
├── Data/
│   └── ErpDbContext.cs            # EF Core DbContext tanımı
├── models/                        # Veri modelleri ve enum'lar
│   ├── Employee.cs
│   ├── LeaveRecord.cs
│   ├── LeaveType.cs
│   ├── LeaveStatus.cs
│   ├── AdvanceExpenseRecord.cs
│   ├── Announcements.cs
│   ├── PayrollCalculationResult.cs
│   ├── WorkStatusType.cs
│   ├── LoginRequest.cs
│   └── LeaveNotificationRequest.cs
├── services/
│   ├── ILeaveCalculationService.cs
│   └── LeaveCalculationService.cs # İzin/maaş kesinti hesaplama mantığı
├── Migrations/                    # EF Core migration geçmişi
├── wwwroot/                       # Statik frontend (tek sayfa uygulama)
│   ├── index.html
│   ├── app.js
│   └── Style.css
├── Program.cs                     # Uygulama giriş noktası ve servis kayıtları
├── appsettings.json
└── ErpPersonelLeaveSystem.csproj
```

## Kurulum

### Gereksinimler

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- (Opsiyonel) SQL Server, SQLite yerine kullanılmak istenirse

### Adımlar

```bash
# Depoyu klonlayın
git clone https://github.com/<kullanici-adiniz>/ErpPersonelLeaveSystem.git
cd ErpPersonelLeaveSystem

# Bağımlılıkları yükleyin
dotnet restore
```

## Çalıştırma

```bash
dotnet run
```

Uygulama ayağa kalktığında:

- Web arayüzü: `http://localhost:<port>/`
- Swagger UI (yalnızca Development ortamında): `http://localhost:<port>/swagger`

Uygulama ilk çalıştırıldığında SQLite veritabanı dosyası (`erp.db`) ve gerekli tablolar otomatik olarak oluşturulur (`Program.cs` içindeki `EnsureCreated()` ve `ExecuteSqlRaw` kuralları sayesinde).

## Veritabanı

- Uygulama varsayılan olarak **SQLite** kullanır; bağlantı `Program.cs` içinde `Data Source=erp.db` olarak sabit tanımlıdır.
- `appsettings.json` dosyasında bir SQL Server bağlantı dizesi (`DefaultConnection`) bulunur; bu, SQL Server'a geçiş yapılmak istendiğinde `Program.cs` içindeki `UseSqlite` çağrısının `UseSqlServer` ile değiştirilmesiyle kullanılabilir. **Not:** Şu anki haliyle bu bağlantı dizesi kodda aktif olarak kullanılmamaktadır.
- EF Core migration'ları `Migrations/` klasöründe yer alır. Migration'ları elle uygulamak isterseniz:

```bash
dotnet ef database update
```

## API Uç Noktaları

Tüm uç noktalar `api/personnel` ön eki altındadır.

| Metot | Yol | Açıklama |
|---|---|---|
| GET | `/api/personnel` | Tüm personelleri listele |
| GET | `/api/personnel/{id}` | Tek personel getir |
| POST | `/api/personnel` | Yeni personel ekle |
| PUT | `/api/personnel/{id}` | Personel bilgilerini güncelle |
| DELETE | `/api/personnel/{id}` | Personel sil |
| POST | `/api/personnel/calculate` | İzin/maaş kesinti simülasyonu yap |
| GET | `/api/personnel/leaves` | Onaylı izin kayıtlarını listele |
| POST | `/api/personnel/add-leave` | Doğrudan (onaylı) izin kaydı ekle |
| POST | `/api/personnel/request-leave` | İzin talebi oluştur |
| GET | `/api/personnel/pending-leaves` | Bekleyen izin taleplerini listele |
| POST | `/api/personnel/approve-leave/{id}` | İzin talebini onayla |
| POST | `/api/personnel/reject-leave/{id}` | İzin talebini reddet |
| POST | `/api/personnel/send-notification` | İzin kaydına bildirim notu gönder |
| POST | `/api/personnel/swipe-card` | RFID kart okutma (giriş/çıkış) simülasyonu |
| POST | `/api/personnel/login` | Personel/yönetici girişi |
| POST | `/api/personnel/import-csv` | CSV/Excel ile toplu personel içe aktar |
| GET | `/api/personnel/export-leaves-csv` | Onaylı izin kayıtlarını CSV olarak indir |
| POST | `/api/personnel/request-advance-expense` | Avans/masraf talebi oluştur |
| GET | `/api/personnel/pending-advances` | Bekleyen avans/masraf taleplerini listele |
| POST | `/api/personnel/approve-advance/{id}` | Avans/masraf talebini onayla |
| POST | `/api/personnel/reject-advance/{id}` | Avans/masraf talebini reddet |
| GET | `/api/personnel/announcements` | Aktif duyuruları listele |
| POST | `/api/personnel/announcements` | Yeni duyuru yayınla |
| DELETE | `/api/personnel/announcements/{id}` | Duyuru sil |
| GET | `/api/personnel/payslip/{leaveId}` | İzin kaydına bağlı bordro verisi getir |

Detaylı istek/yanıt şemaları için uygulamayı Development ortamında çalıştırıp `/swagger` adresini ziyaret edebilirsiniz.

## Roller ve Giriş Mantığı

Sistemde ayrı bir kimlik doğrulama/parola mekanizması bulunmamaktadır. Giriş, personel adı veya ID'si ile yapılır; kullanıcının **Admin** ya da **Employee** rolü, personelin departman alanındaki anahtar kelimelere (`yönetim`, `kurul`, `insan kaynakları`, `ik`, `it`, `direktör`, `ceo` vb.) bakılarak dinamik olarak belirlenir.

> ⚠️ Bu mekanizma bir demo/prototip yaklaşımıdır; gerçek/production kullanım için parola tabanlı kimlik doğrulama, yetkilendirme (JWT/Identity) ve HTTPS zorunluluğu eklenmesi önerilir.

## Bilinen Kısıtlar / Yapılacaklar

- Parola tabanlı kimlik doğrulama yok.
- `appsettings.json` içindeki SQL Server bağlantı dizesi ile `Program.cs` içindeki SQLite yapılandırması senkron değil.
<<<<<<< HEAD
=======
- `erp.db` veritabanı dosyası depoda takip ediliyorsa (`.gitignore` içeriğini kontrol edin), üretim verisiyle karışmaması için deponuzdan çıkarmanız önerilir.
>>>>>>> 00c7e6fb62756781c010a2e1ffbf9538f230bfb7
- Otomatik test (unit/integration test) bulunmuyor.

## Lisans

<<<<<<< HEAD
Bu proje [MIT Lisansı](LICENSE) ile lisanslanmıştır.
=======
Bu proje için bir lisans belirtilmemiştir. Depoya bir `LICENSE` dosyası ekleyerek (ör. MIT) kullanım koşullarını netleştirebilirsiniz.
>>>>>>> 00c7e6fb62756781c010a2e1ffbf9538f230bfb7
