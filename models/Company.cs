using System.ComponentModel.DataAnnotations;

namespace ErpPersonelLeaveSystem.models;

public class Company
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string CompanyCode { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    public string MasterPasswordHash { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string AdminEmail { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
