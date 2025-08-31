using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using FloorPlanAPI.Models;
using FloorPlanAPI.Services;
using StackExchange.Redis;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDbContext<FloorPlanContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddFloorPlanServices(builder.Configuration);

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// Serve static files from web directory
var webPath = Path.Combine(Directory.GetCurrentDirectory(), "web");
if (Directory.Exists(webPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webPath),
        RequestPath = "/web"
    });
}

app.MapGet("/", () => new { 
    message = "AutoCAD Floor Plan Analysis System", 
    version = "1.0.0", 
    swagger = "/swagger",
    endpoints = new[] { "/api/floorplan/upload", "/api/floorplan/process/{jobId}", "/api/floorplan/status/{jobId}", "/api/floorplan/results/{jobId}" }
});

app.MapControllers();
app.Run();

[ApiController]
[Route("api/[controller]")]
public class FloorPlanController : ControllerBase
{
    private readonly IFileStorageService _storageService;
    private readonly IJobQueueService _jobQueueService;
    private readonly IDesignAutomationService _designAutomationService;
    private readonly FloorPlanContext _context;

    public FloorPlanController(
        IFileStorageService storageService,
        IJobQueueService jobQueueService,
        IDesignAutomationService designAutomationService,
        FloorPlanContext context)
    {
        _storageService = storageService;
        _jobQueueService = jobQueueService;
        _designAutomationService = designAutomationService;
        _context = context;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        var allowedExtensions = new[] { ".dwg", ".dxf", ".pdf" };
        var extension = System.IO.Path.GetExtension(file.FileName).ToLower();
        
        if (!allowedExtensions.Contains(extension))
            return BadRequest("Invalid file type. Only DWG, DXF, and PDF files are allowed.");

        try
        {
            Console.WriteLine($"Uploading file: {file.FileName}, Size: {file.Length} bytes");
            
            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            Console.WriteLine($"Generated filename: {fileName}");
            
            var fileUrl = await _storageService.UploadFileAsync(fileName, file.OpenReadStream());
            Console.WriteLine($"File uploaded to: {fileUrl}");

            var job = new ProcessingJob
            {
                Id = Guid.NewGuid().ToString(),
                FileName = file.FileName,
                InputFileUrl = fileUrl,
                Status = JobStatus.Uploaded,
                CreatedAt = DateTime.UtcNow
            };

            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();
            Console.WriteLine($"Job created with ID: {job.Id}");

            return Ok(new { jobId = job.Id, message = "File uploaded successfully" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Upload error: {ex}");
            return StatusCode(500, new { error = ex.Message, details = ex.InnerException?.Message });
        }
    }

    [HttpPost("process/{jobId}")]
    public async Task<IActionResult> ProcessFloorPlan(string jobId, [FromBody] ProcessingSettings settings)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null)
            return NotFound("Job not found");

        try
        {
            job.Status = JobStatus.Queued;
            job.Settings = System.Text.Json.JsonSerializer.Serialize(settings);
            await _context.SaveChangesAsync();

            await _jobQueueService.EnqueueJobAsync(job.Id, settings, job.InputFileUrl);

            return Ok(new { 
                message = "Processing job has been queued",
                estimatedTime = "2-5 minutes depending on load"
            });
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            await _context.SaveChangesAsync();
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("status/{jobId}")]
    public async Task<IActionResult> GetJobStatus(string jobId)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null)
            return NotFound("Job not found");

        return Ok(new {
            status = job.Status.ToString(),
            progress = GetProgressPercentage(job.Status),
            message = GetStatusMessage(job.Status),
            error = job.ErrorMessage
        });
    }

    [HttpGet("results/{jobId}")]
    public async Task<IActionResult> GetResults(string jobId)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null)
            return NotFound("Job not found");

        if (job.Status != JobStatus.Completed)
            return BadRequest("Job not completed");

        try
        {
            var results = await _designAutomationService.GetJobResultsAsync(jobId);

            return Ok(new ProcessResultsResponse
            {
                FinalPlan = new PlanResult
                {
                    DwgUrl = await _storageService.GetSignedUrlAsync(results.FinalPlanDwg, TimeSpan.FromHours(1)),
                    ThumbnailUrl = await _storageService.GetSignedUrlAsync(results.FinalPlanPng, TimeSpan.FromHours(1))
                },
                Measurements = results.Measurements
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private int GetProgressPercentage(JobStatus status)
    {
        return status switch
        {
            JobStatus.Uploaded => 10,
            JobStatus.Queued => 20,
            JobStatus.Processing => 50,
            JobStatus.Completed => 100,
            JobStatus.Failed => 0,
            _ => 0
        };
    }

    private string GetStatusMessage(JobStatus status)
    {
        return status switch
        {
            JobStatus.Uploaded => "File uploaded, ready for processing",
            JobStatus.Queued => "Job is in the processing queue",
            JobStatus.Processing => "Processing floor plan...",
            JobStatus.Completed => "Processing completed successfully",
            JobStatus.Failed => "Processing failed",
            _ => "Unknown status"
        };
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFloorPlanServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IDesignAutomationService, DesignAutomationService>();
        services.AddSingleton<IFileStorageService, AzureBlobStorageService>();
        
        services.AddHostedService<JobProcessorService>();
        
        var redisConnectionString = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            try
            {
                services.AddSingleton<IConnectionMultiplexer>(provider => 
                    ConnectionMultiplexer.Connect(redisConnectionString));
                services.AddSingleton<IJobQueueService, RedisJobQueueService>();
            }
            catch
            {
                services.AddSingleton<IJobQueueService, InMemoryJobQueueService>();
            }
        }
        else
        {
            services.AddSingleton<IJobQueueService, InMemoryJobQueueService>();
        }

        return services;
    }
}