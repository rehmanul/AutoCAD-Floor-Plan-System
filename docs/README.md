# AutoCAD Floor Plan Analysis System

## 1. Project Overview

The AutoCAD Floor Plan Analysis System is a production-grade web application designed to process and analyze AutoCAD floor plan files (`.dwg`, `.dxf`, and `.pdf`). The system automates the extraction of structural elements, the placement of furniture and equipment ("ilots"), and the generation of circulation corridors. It is built on a modern, cloud-native architecture, leveraging Autodesk Platform Services (APS) for core processing, and is designed for scalability, reliability, and ease of deployment.

The application provides a user-friendly web interface for uploading floor plans, configuring processing parameters, and visualizing the results. The backend is powered by a robust set of microservices, orchestrated by Kubernetes, and includes a job queuing system to handle concurrent processing requests.

## 2. System Architecture

The application is composed of several key components, each designed to handle a specific aspect of the processing workflow:

- **Web API**: An ASP.NET Core 8 application that serves as the primary entry point for all client interactions. It manages file uploads, initiates processing jobs, and delivers results to the frontend.

- **AutoCAD Plugin**: A .NET assembly that runs within the AutoCAD 2024 environment. It contains the core logic for analyzing floor plans, placing "ilots," and generating corridors, and is packaged as a Design Automation AppBundle for cloud-based execution.

- **Web Interface**: A single-page application built with vanilla HTML, CSS, and JavaScript, providing a responsive and intuitive user experience.

- **Database**: A PostgreSQL database that stores all job-related information, including status, settings, and file metadata.

- **Job Queue**: A Redis instance that manages and queues concurrent processing jobs, ensuring the system can handle a high volume of requests.

- **File Storage**: An Azure Blob Storage account that stores uploaded floor plans and the resulting output files.

## 3. Production Deployment

The application is designed to be deployed to a Kubernetes cluster, providing a scalable and resilient production environment. The deployment process is fully automated by the `deploy.sh` script, which handles the following steps:

1.  **Build and Push Docker Image**: The script builds a Docker image for the Web API and pushes it to a container registry.

2.  **Deploy Design Automation AppBundle**: The script deploys the AutoCAD plugin to the Autodesk Design Automation service, making it available for cloud-based execution.

3.  **Apply Kubernetes Manifests**: The script applies the Kubernetes manifests, which define the deployment, services, and other resources required to run the application.

### Prerequisites

Before running the deployment script, ensure the following environment variables are configured in your CI/CD environment:

- `FORGE_CLIENT_ID`: Your Autodesk Platform Services client ID.
- `FORGE_CLIENT_SECRET`: Your Autodesk Platform Services client secret.
- `FORGE_NICKNAME`: The nickname of your Design Automation app.
- `AZURE_STORAGE_CONNECTION_STRING`: The connection string for your Azure Blob Storage account.
- `AZURE_STORAGE_CONTAINER`: The name of the container for file storage.
- `REDIS_CONNECTION_STRING`: The connection string for your Redis instance.
- `POSTGRES_CONNECTION_STRING`: The connection string for your PostgreSQL database.
- `DOCKER_REGISTRY_URL`: The URL of your Docker container registry.

### Deployment Steps

To deploy the application, execute the `deploy.sh` script:

```bash
./deploy.sh
```

This script will automate the entire deployment process, from building the Docker image to applying the Kubernetes manifests.

## 4. Development Setup

To set up a local development environment, you will need the following:

- **.NET 8 SDK**: For building and running the Web API and AutoCAD plugin.
- **Docker Desktop**: For running the PostgreSQL and Redis containers.
- **Visual Studio Code**: With the C# extension for a seamless development experience.

Once the prerequisites are installed, you can use the `docker-compose.yml` file in the `deployment_config.txt` to start the required services:

```bash
docker-compose up -d
```

This will start the PostgreSQL and Redis containers, providing a local environment for testing and development.