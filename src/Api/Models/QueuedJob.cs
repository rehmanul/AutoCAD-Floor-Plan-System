using System;

namespace FloorPlanAPI.Models
{
    public class QueuedJob
    {
        public required string JobId { get; set; }
        public required string InputFileUrl { get; set; }
        public required ProcessingSettings Settings { get; set; }
        public DateTime QueuedAt { get; set; }
    }
}