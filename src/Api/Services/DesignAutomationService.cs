using FloorPlanAPI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;

namespace FloorPlanAPI.Services
{
    public interface IDesignAutomationService
    {
        Task<CreateJobResponse> CreateJobAsync(ProcessJobRequest request);
        Task<JobStatusResponse> GetJobStatusAsync(string forgeJobId);
        Task<JobResultsResponse> GetJobResultsAsync(string forgeJobId);
    }

    public class DesignAutomationService : IDesignAutomationService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<DesignAutomationService> _logger;
        private readonly IFileStorageService _storageService;
        private readonly IAdvancedDesignAutomationService _advancedService;

        public DesignAutomationService(IConfiguration config, ILogger<DesignAutomationService> logger, IFileStorageService storageService, IAdvancedDesignAutomationService advancedService)
        {
            _config = config;
            _logger = logger;
            _storageService = storageService;
            _advancedService = advancedService;
        }



        public async Task<CreateJobResponse> CreateJobAsync(ProcessJobRequest request)
        {
            try
            {
                var outputUrl = await _storageService.GetSignedUrlAsync($"results/{request.JobId}/result.pdf", TimeSpan.FromHours(1), true);
                
                var workItemId = await _advancedService.CreateWorkItemAsync(
                    inputFileUrl: request.InputFileUrl,
                    outputFileUrl: outputUrl
                );
                
                return new CreateJobResponse { ForgeJobId = workItemId };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Design Automation job for {JobId}", request.JobId);
                throw;
            }
        }

        public async Task<JobStatusResponse> GetJobStatusAsync(string forgeJobId)
        {
            try
            {
                var (status, reportUrl) = await _advancedService.WaitForCompletionAsync(forgeJobId, TimeSpan.FromSeconds(1));
                
                var isCompleted = !status.Equals("pending", StringComparison.OrdinalIgnoreCase) &&
                                !status.Equals("inprogress", StringComparison.OrdinalIgnoreCase);
                var isSuccessful = status.Equals("success", StringComparison.OrdinalIgnoreCase);

                return new JobStatusResponse
                {
                    IsCompleted = isCompleted,
                    IsSuccessful = isSuccessful,
                    ErrorMessage = isSuccessful ? null : $"Job status: {status}. Report: {reportUrl}"
                };
            }
            catch (TimeoutException)
            {
                // Still in progress
                return new JobStatusResponse
                {
                    IsCompleted = false,
                    IsSuccessful = false,
                    ErrorMessage = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get job status for {JobId}", forgeJobId);
                return new JobStatusResponse
                {
                    IsCompleted = true,
                    IsSuccessful = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<JobResultsResponse> GetJobResultsAsync(string jobId)
        {
            try
            {
                var measurementsJson = await _storageService.DownloadFileAsync($"results/{jobId}/measurements.json");
                var measurements = await JsonSerializer.DeserializeAsync<MeasurementData>(measurementsJson);

                return new JobResultsResponse
                {
                    FinalPlanDwg = await _storageService.GetSignedUrlAsync($"results/{jobId}/final_plan.dwg", TimeSpan.FromHours(1), false),
                    FinalPlanPng = await _storageService.GetSignedUrlAsync($"results/{jobId}/final_plan.png", TimeSpan.FromHours(1), false),
                    Measurements = measurements!
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get job results for {JobId}", jobId);
                throw;
            }
        }
    }

    public class TokenResponse
    {
        public string access_token { get; set; } = string.Empty;
        public string token_type { get; set; } = string.Empty;
        public int expires_in { get; set; }
    }

    public class JobResponse
    {
        public string id { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string? statusDetails { get; set; }
    }
}
