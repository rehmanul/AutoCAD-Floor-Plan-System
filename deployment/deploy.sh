#!/bin/bash
# Production-Ready Deployment Script

# Exit immediately if a command exits with a non-zero status.
set -e

# === CONFIGURATION ===
# Ensure the following environment variables are set in your CI/CD environment:
# FORGE_CLIENT_ID: Your Autodesk Platform Services client ID.
# FORGE_CLIENT_SECRET: Your Autodesk Platform Services client secret.
# FORGE_NICKNAME: The nickname of your Design Automation app.
# AZURE_STORAGE_CONNECTION_STRING: The connection string for your Azure Blob Storage account.
# AZURE_STORAGE_CONTAINER: The name of the container for file storage.
# REDIS_CONNECTION_STRING: The connection string for your Redis instance.
# POSTGRES_CONNECTION_STRING: The connection string for your PostgreSQL database.
# DOCKER_REGISTRY_URL: The URL of your Docker container registry.

# === DEPLOYMENT FUNCTIONS ===

# 1. Build and push the Docker image for the Web API
build_and_push_api() {
    echo "Building and pushing Web API Docker image..."
    docker build -t "$DOCKER_REGISTRY_URL/floorplan-api:latest" -f api/Dockerfile .
    docker push "$DOCKER_REGISTRY_URL/floorplan-api:latest"
    echo "Web API image pushed successfully."
}

# 2. Deploy the Design Automation AppBundle
deploy_appbundle() {
    echo "Deploying Design Automation AppBundle..."
    
    # Authenticate with APS
    local access_token=$(curl -s -X POST "https://developer.api.autodesk.com/authentication/v1/authenticate" \
        -H "Content-Type: application/x-www-form-urlencoded" \
        -d "client_id=$FORGE_CLIENT_ID&client_secret=$FORGE_CLIENT_SECRET&grant_type=client_credentials&scope=code:all" | jq -r .access_token)

    # Create AppBundle
    curl -s -X POST "https://developer.api.autodesk.com/da/us-east/v3/appbundles" \
        -H "Authorization: Bearer $access_token" -H "Content-Type: application/json" \
        -d '{"id": "FloorPlanProcessor","engine": "Autodesk.AutoCAD+24"}'

    # Upload AppBundle zip
    curl -s -X POST "https://developer.api.autodesk.com/da/us-east/v3/appbundles/FloorPlanProcessor/versions" \
        -H "Authorization: Bearer $access_token" -F "file=@autocad_plugin/FloorPlanProcessor.zip"

    # Create Activity
    curl -s -X POST "https://developer.api.autodesk.com/da/us-east/v3/activities" \
        -H "Authorization: Bearer $access_token" -H "Content-Type: application/json" \
        -d @da_config.json

    echo "AppBundle deployed successfully."
}

# 3. Apply Kubernetes manifests
deploy_kubernetes() {
    echo "Deploying to Kubernetes..."
    envsubst < deployment/kubernetes.yaml | kubectl apply -f -
    kubectl rollout status deployment/floorplan-api
    echo "Kubernetes deployment successful."
}

# === MAIN DEPLOYMENT WORKFLOW ===
main() {
    echo "Starting production deployment..."
    
    build_and_push_api
    deploy_appbundle
    deploy_kubernetes
    
    echo "Deployment completed successfully!"
}

main