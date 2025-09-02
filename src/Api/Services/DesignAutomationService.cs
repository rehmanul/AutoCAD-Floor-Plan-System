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
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _baseUrl = "https://developer.api.autodesk.com/da/us-east/v3";

        public DesignAutomationService(IConfiguration config, ILogger<DesignAutomationService> logger, IFileStorageService storageService)
        {
            _config = config;
            _logger = logger;
            _storageService = storageService;
            _httpClient = new HttpClient();
            _clientId = _config["Forge:ClientId"] ?? Environment.GetEnvironmentVariable("FORGE_CLIENT_ID") ?? "bZCKOFynve2w4rpzNYmooBYAGuqxKWelBTiGcfdoSUpVlD0r";
            _clientSecret = _config["Forge:ClientSecret"] ?? Environment.GetEnvironmentVariable("FORGE_CLIENT_SECRET") ?? "QusNbDYeB6WFl9vzDSq16Gcpbz7rJO2tIMcJBTBV0ro0GRrS2O9s4gRPzT1uVSoS";
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var tokenUrl = "https://developer.api.autodesk.com/authentication/v2/token";
            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", "code:all")
            });

            var response = await _httpClient.PostAsync(tokenUrl, requestBody);
            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content);
            return tokenResponse!.access_token;
        }

        public async Task<CreateJobResponse> CreateJobAsync(ProcessJobRequest request)
        {
            var token = await GetAccessTokenAsync();
            var settingsJson = JsonSerializer.Serialize(request.Settings);
            var settingsUrl = await _storageService.UploadFileAsync($"settings_{request.JobId}.json", new MemoryStream(Encoding.UTF8.GetBytes(settingsJson)));

            var jobPayload = new
            {
                activityId = "bZCKOFynve2w4rpzNYmooBYAGuqxKWelBTiGcfdoSUpVlD0r.SimpleFloorPlan+1",
                arguments = new Dictionary<string, object>
                {
                    { "inputFile", new { url = request.InputFileUrl } },
                    { "outputFile", new { verb = "put", url = await _storageService.GetSignedUrlAsync($"results/{request.JobId}/result.dwg", TimeSpan.FromHours(1), true) } }
                }
            };

            var json = JsonSerializer.Serialize(jobPayload);
            _logger.LogInformation("Sending job payload: {Payload}", json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            
            var response = await _httpClient.PostAsync($"{_baseUrl}/jobs", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to create job: {Response}", responseContent);
                throw new Exception($"Failed to create Design Automation job: {responseContent}");
            }

            var jobResponse = JsonSerializer.Deserialize<JobResponse>(responseContent);
            _logger.LogInformation("Design Automation job created: {JobId}", jobResponse!.id);
            
            return new CreateJobResponse { ForgeJobId = jobResponse.id };
        }

        public async Task<JobStatusResponse> GetJobStatusAsync(string forgeJobId)
        {
            var token = await GetAccessTokenAsync();
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            
            var response = await _httpClient.GetAsync($"{_baseUrl}/jobs/{forgeJobId}");
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get job status: {Response}", content);
                return new JobStatusResponse { IsCompleted = true, IsSuccessful = false, ErrorMessage = "Failed to get job status" };
            }

            var job = JsonSerializer.Deserialize<JobResponse>(content);
            var isCompleted = job!.status == "success" || job.status == "failedExecution" || job.status == "cancelled";
            var isSuccessful = job.status == "success";

            return new JobStatusResponse
            {
                IsCompleted = isCompleted,
                IsSuccessful = isSuccessful,
                ErrorMessage = isSuccessful ? null : $"Job failed with status: {job.status}"
            };
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
