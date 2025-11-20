using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpinMonitor.Api.Models
{
    public class Detection
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [Required]
        [MaxLength(255)]
        public string Stream { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? StreamType { get; set; }

        [MaxLength(50)]
        public string? StreamNumber { get; set; }

        [MaxLength(500)]
        public string? Track { get; set; }

        public int? DurationSeconds { get; set; }

        public decimal? Confidence { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
