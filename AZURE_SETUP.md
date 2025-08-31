# Azure Storage Setup for Production

## Create Azure Storage Account:

1. **Go to [portal.azure.com](https://portal.azure.com)**
2. **Create Resource** → **Storage Account**
3. **Settings:**
   - Name: `autocadfloorplanstorage` (must be unique)
   - Performance: Standard
   - Redundancy: LRS (cheapest)
   - Region: East US
4. **Create**

## Get Connection String:

1. **Go to your Storage Account**
2. **Security + networking** → **Access keys**
3. **Copy "Connection string"** from key1

**Format will be:**
```
DefaultEndpointsProtocol=https;AccountName=autocadfloorplanstorage;AccountKey=REAL_KEY_HERE;EndpointSuffix=core.windows.net
```

## Update Environment Variable:

Replace the current `ConnectionStrings__AzureStorage` with the real connection string.

## Benefits of Real Azure Storage:
✅ **Unlimited scale** - Handle thousands of files
✅ **Global CDN** - Fast file access worldwide  
✅ **99.9% uptime** - Enterprise reliability
✅ **Automatic backups** - Data protection
✅ **Cost effective** - Pay only for what you use (~$0.02/GB)

## Alternative: Keep Local Storage
If you prefer to start simple:
- Local storage works fine for **hundreds of files**
- Easy to migrate to Azure later
- No additional costs
- Files stored on Render's disk

**Recommendation:** Start with local storage, migrate to Azure when you need scale.