using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpPersonelLeaveSystem.models;

public class LeaveRecord
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
    public LeaveType LeaveType { get; set; }

    [Required]
    public decimal LeaveDays { get; set; } = 1;

    [Required]
    public DateTime StartDate { get; set; } = DateTime.Now;

    [Required]
    public DateTime EndDate { get; set; } = DateTime.Now.AddDays(1);

    public decimal CalculatedDeducation { get; set; }

    public decimal FinalSalary { get; set; }

    [MaxLength(250)]
    public string? Note { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Approved;
}