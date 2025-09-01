using FloorPlanAPI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FloorPlanAPI.Services
{
    public class JobProcessorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<JobProcessorService> _logger;

        public JobProcessorService(IServiceProvider serviceProvider, ILogger<JobProcessorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Job processor service started");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(60000, stoppingToken); // Check every minute
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in job processor");
                    await Task.Delay(10000, stoppingToken);
                }
            }
            
            _logger.LogInformation("Job processor service stopped");
        }
    }
}