using System.ComponentModel.DataAnnotations;

namespace ErpPersonelLeaveSystem.models;

public class Announcement
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "Genel"; // "İK Duyurusu", "İdari İşler", "Etkinlik", "Resmi Genelge"

    [Required]
    [MaxLength(20)]
    public string Priority { get; set; } = "Normal"; // "Normal", "Önemli", "Acil"

    public string PublisherName { get; set; } = "İnsan Kaynakları / Yönetim";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsActive { get; set; } = true;
}
