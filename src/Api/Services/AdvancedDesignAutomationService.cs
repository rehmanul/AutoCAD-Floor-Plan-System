using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FloorPlanAPI.Services
{
    public interface IAdvancedDesignAutomationService
    {
        Task<string> CreateWorkItemAsync(string inputFileUrl, string outputFileUrl, Dictionary<string, string>? outputHeaders = null);
        Task<(string Status, string? ReportUrl)> WaitForCompletionAsync(string workItemId, TimeSpan timeout);
    }

    public class AdvancedDesignAutomationService : IAdvancedDesignAutomationService
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _region;
        private readonly HttpClient _http;
        private readonly ILogger<AdvancedDesignAutomationService> _logger;

        public AdvancedDesignAutomationService(IConfiguration config, ILogger<AdvancedDesignAutomationService> logger)
        {
            _clientId = config["Forge:ClientId"] ?? Environment.GetEnvironmentVariable("FORGE_CLIENT_ID") ?? "bZCKOFynve2w4rpzNYmooBYAGuqxKWelBTiGcfdoSUpVlD0r";
            _clientSecret = config["Forge:ClientSecret"] ?? Environment.GetEnvironmentVariable("FORGE_CLIENT_SECRET") ?? "QusNbDYeB6WFl9vzDSq16Gcpbz7rJO2tIMcJBTBV0ro0GRrS2O9s4gRPzT1uVSoS";
            _region = config["Forge:Region"] ?? "us-east";
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            _logger = logger;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["grant_type"] = "client_credentials",
                ["scope"] = "code:all data:read data:write bucket:create bucket:read"
            });

            using var resp = await _http.PostAsync("https://developer.api.autodesk.com/authentication/v2/token", content);
            var body = await resp.Content.ReadAsStringAsync();
            
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get access token: {StatusCode} {Body}", resp.StatusCode, body);
                throw new InvalidOperationException($"Failed to get access token: {resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("access_token").GetString()!;
        }

        public async Task<string> CreateWorkItemAsync(string inputFileUrl, string outputFileUrl, Dictionary<string, string>? outputHeaders = null)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = $"https://developer.api.autodesk.com/da/{_region}/v3/workitems";

            var payload = new
            {
                activityId = "AutoCAD.PlotToPDF+25_0",
                arguments = new Dictionary<string, object?>
                {
                    ["HostDwg"] = new { url = inputFileUrl },
                    ["Result"] = outputHeaders == null
                        ? new { verb = "put", url = outputFileUrl }
                        : new { verb = "put", url = outputFileUrl, headers = outputHeaders }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            _logger.LogInformation("Creating workitem with payload: {Payload}", JsonSerializer.Serialize(payload));

            using var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Workitem creation failed: {StatusCode} {Body}", resp.StatusCode, body);
                throw new InvalidOperationException($"Workitem creation failed: {resp.StatusCode} {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var workItemId = doc.RootElement.GetProperty("id").GetString()!;
            _logger.LogInformation("Workitem created: {WorkItemId}", workItemId);
            return workItemId;
        }

        public async Task<(string Status, string? ReportUrl)> WaitForCompletionAsync(string workItemId, TimeSpan timeout)
        {
            var token = await GetAccessTokenAsync();
            var baseUrl = $"https://developer.api.autodesk.com/da/{_region}/v3/workitems/{workItemId}";
            var start = DateTimeOffset.UtcNow;
            var pollInterval = TimeSpan.FromSeconds(5);

            while (true)
            {
                if (DateTimeOffset.UtcNow - start > timeout)
                    throw new TimeoutException("Workitem polling timed out.");

                using var req = new HttpRequestMessage(HttpMethod.Get, baseUrl);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var resp = await _http.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();
                
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("Status fetch failed: {StatusCode} {Body}", resp.StatusCode, body);
                    throw new InvalidOperationException($"Status fetch failed: {resp.StatusCode} {body}");
                }

                using var doc = JsonDocument.Parse(body);
                var status = doc.RootElement.GetProperty("status").GetString()!;
                string? reportUrl = doc.RootElement.TryGetProperty("reportUrl", out var r) ? r.GetString() : null;

                _logger.LogInformation("Workitem {WorkItemId} status: {Status}", workItemId, status);
                
                if (!status.Equals("pending", StringComparison.OrdinalIgnoreCase) &&
                    !status.Equals("inprogress", StringComparison.OrdinalIgnoreCase))
                    return (status, reportUrl);

                await Task.Delay(pollInterval);
            }
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}