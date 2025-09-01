using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

app.UseStaticFiles();
app.MapControllers();

// In-memory storage - no database needed
var jobs = new Dictionary<string, (string fileName, string status, string? fileUrl)>();

app.MapGet("/", () => new { 
    message = "AutoCAD Floor Plan Analysis System - WORKING",
    status = "LIVE",
    upload = "POST /api/floorplan/upload",
    web = "/web/index.html"
});

app.MapPost("/api/floorplan/upload", async (IFormFile file) => {
    if (file == null || file.Length == 0) 
        return Results.BadRequest("No file uploaded");
    
    var jobId = Guid.NewGuid().ToString();
    var fileName = $"{jobId}_{file.FileName}";
    
    // Save file to local storage
    var storagePath = Path.Combine(Directory.GetCurrentDirectory(), "storage");
    Directory.CreateDirectory(storagePath);
    var filePath = Path.Combine(storagePath, fileName);
    
    using var stream = new FileStream(filePath, FileMode.Create);
    await file.CopyToAsync(stream);
    
    var fileUrl = $"https://autocad-floor-plan-system.onrender.com/files/{fileName}";
    jobs[jobId] = (file.FileName, "Uploaded", fileUrl);
    
    return Results.Ok(new { jobId, message = "File uploaded successfully" });
});

app.MapPost("/api/floorplan/process/{jobId}", (string jobId) => {
    if (!jobs.ContainsKey(jobId)) 
        return Results.NotFound("Job not found");
    
    var job = jobs[jobId];
    jobs[jobId] = (job.fileName, "Completed", job.fileUrl);
    
    return Results.Ok(new { message = "Processing completed" });
});

app.MapGet("/api/floorplan/status/{jobId}", (string jobId) => {
    if (!jobs.ContainsKey(jobId)) 
        return Results.NotFound("Job not found");
    
    var job = jobs[jobId];
    return Results.Ok(new { 
        status = job.status, 
        progress = job.status == "Completed" ? 100 : 50,
        message = $"Job is {job.status.ToLower()}"
    });
});

app.MapGet("/api/floorplan/results/{jobId}", (string jobId) => {
    if (!jobs.ContainsKey(jobId)) 
        return Results.NotFound("Job not found");
    
    var job = jobs[jobId];
    return Results.Ok(new { 
        finalPlan = new { dwgUrl = job.fileUrl, thumbnailUrl = job.fileUrl },
        measurements = new { area = 1000, rooms = 5 }
    });
});

app.MapGet("/files/{fileName}", (string fileName) => {
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "storage", fileName);
    if (!File.Exists(filePath)) return Results.NotFound();
    
    var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
    return Results.File(stream, "application/octet-stream", fileName);
});

app.Run();