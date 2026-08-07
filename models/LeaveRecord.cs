using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpPersonelLeaveSystem.models
{
    public class LeaveRecord
    {
        [Key]
        public int Id { get; set; }

        //hangi çalışanın izin kullandığını belirten foreignkey

        // EF core ilişkisi: izin kaydının ait olduğu Personel nesnesine erişim sağlar
        public int employeeId { get; set; }
        [ForeignKey(nameof(employeeId))]
        public Employee? employee { get; set; }

        //kullanılan izin türü 
        public LeaveType LeaveType { get; set; } //kendimize enum LeaveType diyerek özel bir değişken tipi oluşturmuştuk.

        //kullanılan izin günü sayısı
        public int LeaveDays { get; set; }

        //izin talep tarihi (Sistem tarafından varsayılan olarak belirlenir, kullanıcı kayıt oluştururken taih belirtmesine/belirtmemesine bakılmaksızın tam o saniyedeki sunucu/bilgisayar saatini ve tarihini otomatik olarak bu alana yazar 

        //izin açıklaması (? işareti boş bırakılabilir demektir)
        [StringLength(250)]
        public string? Note { get; set; }

        //izin nedeniyle yapılan kesinti tutarı 
        [Column(TypeName = "decimal(18,2)")]
        public decimal CalculatedDeducation { get; set; }

        //kesinti sonrası hesaplanan o ayki net maaş
        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalSalary { get; set; }

    }

}




