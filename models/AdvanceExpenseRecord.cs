using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpPersonelLeaveSystem.models;

public enum RequestCategoryType
{
    Advance = 1, // Maaş Avansı (Maaştan Düşer)
    Expense = 2  // Masraf / Harcırah İadesi (Maaşa Eklenir)
}

public enum RequestStatusType
{
    Pending = 0,  // Onay Bekliyor
    Approved = 1, // Onaylandı
    Rejected = 2  // Reddedildi
}

public class AdvanceExpenseRecord
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int CompanyId { get; set; }

    [Required]
    public int employeeId { get; set; }

    [ForeignKey("employeeId")]
    public Employee? employee { get; set; }

    [Required]
    public RequestCategoryType Type { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(250)]
    public string Description { get; set; } = string.Empty;

    public RequestStatusType Status { get; set; } = RequestStatusType.Pending;

    public DateTime RequestDate { get; set; } = DateTime.Now;
}
