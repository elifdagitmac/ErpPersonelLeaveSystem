//personeller tablosu null bir şekilde oluşturuldu.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text.Json.Serialization;

namespace ErpPersonelLeaveSystem.models
{
    
    public class Employee
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CompanyId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(150)]
        public string? Email { get; set; }

        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        // Sadece istek gövdesinde gelen düz metin şifreyi taşır, veritabanına yazılmaz, yanıtlarda dönmez.
        [NotMapped]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Password { get; set; }

        [Required]
        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        public int ExperienceYears { get; set; }

        [StringLength(50)]
        public string EducationLevel { get; set; } = string.Empty;

        public int Age { get; set; }

        [StringLength(20)]
        public string Gender { get; set; } = string.Empty;

        [Column(TypeName = " decimal(18,2)")] 
        public decimal MonthlySalary { get; set; }
        /*diğer alanlar varsayılan SQL tipleriyle otomatik eşleşir, 
        sadece decimal tipinin virgülden sonraki hassasiyet kuralını özel olarak tanımlamak için column kullandık*/

        //Çalışma durumu ( ofis, remote, izinli, giriş yapılmadı)
        public WorkStatusType WorkStatus { get; set; } = WorkStatusType.Ofiste;











    }
}
