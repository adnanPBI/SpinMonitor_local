using System.ComponentModel.DataAnnotations;

namespace SpinMonitor.Api.Models.DTOs
{
    public class CreateDetectionRequest
    {
        [Required]
        public string Timestamp { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Stream { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Stream_Type { get; set; }

        [MaxLength(50)]
        public string? Stream_Number { get; set; }

        [MaxLength(500)]
        public string? Track { get; set; }

        public int? Duration_Seconds { get; set; } = 10;

        public double? Confidence { get; set; }
    }

    public class BatchDetectionRequest
    {
        [Required]
        public List<CreateDetectionRequest> Detections { get; set; } = new();
    }
}
