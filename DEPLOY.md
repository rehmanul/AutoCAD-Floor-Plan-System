# Deploy to Render.com

## Prerequisites
1. Create account on [Render.com](https://render.com)
2. Create Azure Storage account (or use existing)

## Steps

### 1. Push to GitHub
```bash
git init
git add .
git commit -m "Initial commit"
git branch -M main
git remote add origin https://github.com/yourusername/floorplan-system.git
git push -u origin main
```

### 2. Create Render Services

#### Database
1. Go to Render Dashboard
2. Click "New" → "PostgreSQL"
3. Name: `floorplan-db`
4. Database: `floorplan_prod`
5. User: `floorplan_user`
6. Plan: Free
7. Create Database

#### Redis
1. Click "New" → "Redis"
2. Name: `floorplan-redis`
3. Plan: Free
4. Create Redis

#### Web Service
1. Click "New" → "Web Service"
2. Connect your GitHub repo
3. Name: `floorplan-api`
4. Environment: Docker
5. Plan: Free
6. Add Environment Variables:
   - `ASPNETCORE_ENVIRONMENT` = `Production`
   - `ASPNETCORE_URLS` = `http://0.0.0.0:10000`
   - `Forge__ClientId` = `bZCKOFynve2w4rpzNYmooBYAGuqxKWelBTiGcfdoSUpVlD0r`
   - `Forge__ClientSecret` = `QusNbDYeB6WFl9vzDSq16Gcpbz7rJO2tIMcJBTBV0ro0GRrS2O9s4gRPzT1uVSoS`
   - `ConnectionStrings__AzureStorage` = Your Azure Storage connection string

### 3. Update Activity for Production
Once deployed, update the activity to use the Render URL:

```bash
# Replace localhost URLs with your Render URL
# Example: https://floorplan-api.onrender.com
```

## Your Production URLs
- **API**: `https://floorplan-api.onrender.com`
- **Web UI**: `https://floorplan-api.onrender.com/web/index.html`
- **Swagger**: `https://floorplan-api.onrender.com/swagger`

## Benefits
✅ **Public URLs** - Forge can access your storage  
✅ **Managed Database** - PostgreSQL included  
✅ **Managed Redis** - Redis included  
✅ **SSL/HTTPS** - Automatic certificates  
✅ **Auto-deploy** - Updates on git push  
✅ **Free Tier** - No cost for development  

The system will be fully production-ready with real CAD processing!