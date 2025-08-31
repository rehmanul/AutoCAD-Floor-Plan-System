using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FloorPlanAPI.Models
{
    public class ProcessJobRequest
    {
        public required string JobId { get; set; }
        public required string InputFileUrl { get; set; }
        public required ProcessingSettings Settings { get; set; }
    }

    public class ProcessingSettings
    {
        public required List<BoxSizeDistribution> BoxDistribution { get; set; }
        public double CorridorWidth { get; set; } = 1200;
    }

    public class BoxSizeDistribution
    {
        public double Percentage { get; set; }
        public double MinArea { get; set; }
        public double MaxArea { get; set; }
    }

    public class CreateJobResponse
    {
        public required string ForgeJobId { get; set; }
    }

    public class JobStatusResponse
    {
        public bool IsCompleted { get; set; }
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class JobResultsResponse
    {
        public required string FinalPlanDwg { get; set; }
        public required string FinalPlanPng { get; set; }
        public required MeasurementData Measurements { get; set; }
    }

    public class ProcessResultsResponse
    {
        public required PlanResult FinalPlan { get; set; }
        public required MeasurementData Measurements { get; set; }
    }

    public class PlanResult
    {
        public required string DwgUrl { get; set; }
        public required string ThumbnailUrl { get; set; }
    }

    public class MeasurementData
    {
        public double TotalArea { get; set; }
        public double WalkableArea { get; set; }
        public double IlotArea { get; set; }
        public double CorridorArea { get; set; }
        public int NumberOfIlots { get; set; }
        public double CorridorLength { get; set; }
    }
}