using System;
using System.ComponentModel.DataAnnotations;

namespace FloorPlanAPI.Models
{
    public class ProcessingJob
    {
        public required string Id { get; set; }
        public required string FileName { get; set; }
        public required string InputFileUrl { get; set; }
        public JobStatus Status { get; set; }
        public string? ForgeJobId { get; set; }
        public string? Settings { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public enum JobStatus
    {
        Uploaded,
        Queued,
        Processing,
        Completed,
        Failed
    }
}