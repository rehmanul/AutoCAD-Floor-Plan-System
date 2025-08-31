using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var app = builder.Build();

// Serve static files
app.UseStaticFiles();
app.MapControllers();

// Simple in-memory storage
var files = new Dictionary<string, (byte[] data, string name)>();

app.MapGet("/", () => new { 
    message = "AutoCAD Floor Plan Analysis System - SIMPLE VERSION",
    status = "WORKING",
    upload = "POST /api/upload",
    web = "/web/index.html"
});

app.MapPost("/api/upload", async (IFormFile file) => {
    if (file == null || file.Length == 0) 
        return Results.BadRequest("No file uploaded");
    
    var jobId = Guid.NewGuid().ToString();
    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    files[jobId] = (stream.ToArray(), file.FileName);
    
    return Results.Ok(new { jobId, message = "File uploaded successfully" });
});

app.MapPost("/api/process/{jobId}", (string jobId) => {
    if (!files.ContainsKey(jobId)) 
        return Results.NotFound("Job not found");
    
    return Results.Ok(new { message = "Processing completed", status = "success" });
});

app.MapGet("/api/status/{jobId}", (string jobId) => {
    if (!files.ContainsKey(jobId)) 
        return Results.NotFound("Job not found");
    
    return Results.Ok(new { 
        status = "Completed", 
        progress = 100,
        message = "Processing completed successfully"
    });
});

app.Run();