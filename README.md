# AutoCAD Floor Plan Analysis System

A production-ready system for processing AutoCAD floor plans with automated analysis and measurements.

## Features

- File upload support for DWG, DXF, and PDF files
- Asynchronous job processing with Redis queue
- Azure Blob Storage integration
- PostgreSQL database for job tracking
- RESTful API with Swagger documentation
- Background job processor service

## Prerequisites

- .NET 8.0 SDK
- PostgreSQL database
- Redis server
- Azure Storage Account (or Azure Storage Emulator for development)
- Autodesk Forge credentials (optional for Design Automation)

## Quick Start

1. **Clone and build the project:**
   ```bash
   git clone <repository-url>
   cd "AutoCAD Floor Plan Analysis System"
   dotnet build src/Api/FloorPlanAPI.csproj
   ```

2. **Configure the application:**
   - Update `src/Api/appsettings.json` with your database and storage credentials
   - For development, use `src/Api/appsettings.Development.json`

3. **Run locally:**
   ```bash
   # Windows
   run-local.bat
   
   # Or manually
   cd src/Api
   dotnet run --urls "http://localhost:5000"
   ```

4. **Access the API:**
   - API: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger

## API Endpoints

### Upload File
```
POST /api/floorplan/upload
Content-Type: multipart/form-data
```

### Process Floor Plan
```
POST /api/floorplan/process/{jobId}
Content-Type: application/json

{
  "boxDistribution": [
    {
      "percentage": 60,
      "minArea": 10,
      "maxArea": 50
    }
  ],
  "corridorWidth": 1200
}
```

### Get Job Status
```
GET /api/floorplan/status/{jobId}
```

### Get Results
```
GET /api/floorplan/results/{jobId}
```

## Configuration

### Database Connection
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=floorplan_db;Username=postgres;Password=your_password"
  }
}
```

### Redis Configuration
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

### Azure Storage
```json
{
  "ConnectionStrings": {
    "AzureStorage": "DefaultEndpointsProtocol=https;AccountName=your_account;AccountKey=your_key;EndpointSuffix=core.windows.net"
  },
  "Storage": {
    "ContainerName": "floorplan-files"
  }
}
```

## Architecture

- **API Layer**: ASP.NET Core Web API with controllers
- **Service Layer**: Business logic and external integrations
- **Data Layer**: Entity Framework Core with PostgreSQL
- **Queue System**: Redis for job queuing
- **Storage**: Azure Blob Storage for file management
- **Background Processing**: Hosted service for job processing

## Production Deployment

1. **Database Setup:**
   ```bash
   dotnet ef database update --project src/Api
   ```

2. **Environment Variables:**
   Set production connection strings and credentials as environment variables

3. **Docker Deployment:**
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/aspnet:8.0
   COPY . /app
   WORKDIR /app
   EXPOSE 80
   ENTRYPOINT ["dotnet", "FloorPlanAPI.dll"]
   ```

4. **Health Checks:**
   The API includes built-in health monitoring endpoints

## Monitoring

- Structured logging with Serilog
- Application Insights integration ready
- Health check endpoints
- Performance counters

## Security

- CORS configuration
- Input validation
- File type restrictions
- Secure file storage with signed URLs
- Environment-based configuration

## Support

For issues and questions, please check the API documentation at `/swagger` when running the application.