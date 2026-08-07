//personeller tablosu null bir şekilde oluşturuldu.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;

namespace ErpPersonelLeaveSystem.models
{
    
    public class Employee
    {
        [Key] 
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        public int ExperienceYears { get; set; }

        [StringLength(50)]
        public string EducationLevek { get; set; } = string.Empty;

        public int Age { get; set; }

        [StringLength(20)]
        public string Gender { get; set; } = string.Empty;

        [Column(TypeName = " decimal(18,2)")] 
        public decimal MonthlySalary { get; set; }
        /*diğer alanlar varsayılan SQL tipleriyle otomatik eşleşir, 
        sadece decimal tipinin virgülden sonraki hassasiyet kuralını özel olarak tanımlamak için column kullandık*/











    }
}
