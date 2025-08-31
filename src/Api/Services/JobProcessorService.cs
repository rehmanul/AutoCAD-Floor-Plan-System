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
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var queueService = scope.ServiceProvider.GetRequiredService<IJobQueueService>();
                    var designAutomation = scope.ServiceProvider.GetRequiredService<IDesignAutomationService>();
                    var context = scope.ServiceProvider.GetRequiredService<FloorPlanContext>();

                    var queuedJob = await queueService.DequeueJobAsync();
                    if (queuedJob != null)
                    {
                        _logger.LogInformation("Processing job: {JobId}", queuedJob.JobId);
                        
                        var job = await context.Jobs.FindAsync(queuedJob.JobId);
                        if(job == null) continue;

                        job.Status = JobStatus.Processing;
                        await context.SaveChangesAsync(stoppingToken);
                        
                        var request = new ProcessJobRequest
                        {
                            JobId = queuedJob.JobId,
                            InputFileUrl = queuedJob.InputFileUrl,
                            Settings = queuedJob.Settings
                        };
                        
                        var daResponse = await designAutomation.CreateJobAsync(request);
                        job.ForgeJobId = daResponse.ForgeJobId;
                        await context.SaveChangesAsync(stoppingToken);

                        // Poll for completion
                        var completed = false;
                        while(!completed)
                        {
                            await Task.Delay(5000, stoppingToken);
                            var status = await designAutomation.GetJobStatusAsync(job.ForgeJobId);
                            if(status.IsCompleted)
                            {
                                job.Status = status.IsSuccessful ? JobStatus.Completed : JobStatus.Failed;
                                job.ErrorMessage = status.ErrorMessage;
                                job.CompletedAt = DateTime.UtcNow;
                                await context.SaveChangesAsync(stoppingToken);
                                completed = true;
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(5000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in job processor");
                    await Task.Delay(10000, stoppingToken);
                }
            }
        }
    }
}