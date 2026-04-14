# ISAPI Worker Process Recycle - Diagnostic Guide

**Error**: "An ISAPI reported an unhealthy condition to its worker process. Therefore, the worker process with process id of '21972' serving application pool 'site63026' has requested a recycle."

This error occurs when the application crashes on startup or becomes unresponsive, causing IIS to recycle the worker process.

---

## 🔍 **Step 1: Check runasp.net Event Logs**

### **Method A: Check Event Viewer (Recommended)**
1. Log into **runasp.net Dashboard**
2. Click **Manage** → **Control Panel**
3. Navigate to **Event Viewer** or **Application Logs**
4. Filter for errors from the last 2 hours
5. Look for entries starting with:
   - `System.Exception`
   - `System.Data.SqlClient`
   - `System.InvalidOperationException`
   - `System.NullReferenceException`

### **Method B: Check Detailed Logs**
```
Look for log files at:
- C:\inetpub\logs\LogFiles\
- C:\Windows\System32\LogFiles\
```

### **Copy & Paste Any Error Messages** 
Share the full stack trace with line numbers from the event logs.

---

## 🛠️ **Step 2: Database Connection Check**

### **Is your database online?**
```sql
-- Connect to your SQL Server database and run:
SELECT GETDATE() AS CurrentTime;
```

If this fails:
- ❌ Database is offline or credentials are wrong
- ✅ Database is online and accessible

**Common runasp.net issues:**
- Connection string points to localhost instead of the actual database server
- Database credentials expired
- Database is on a different server than expected

### **Check Connection String**
In `appsettings.json` (production):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PASSWORD;"
  }
}
```

Make sure:
- ✅ `Server` is NOT `localhost` or `.` (these won't work on runasp.net)
- ✅ `Database`, `User Id`, and `Password` are correct
- ✅ No special characters in password require escaping

---

## 🔧 **Step 3: Clear Old Migrations (If Stuck in Migration Loop)**

If you keep getting recycles, migrations might be failing:

### **Option A: Skip Migrations (Temporary)**
Set in `appsettings.json`:
```json
{
  "Startup": {
    "ApplyEfMigrations": false
  }
}
```

Then redeploy. If this fixes it, migrations are the problem.

### **Option B: Check Migration Status**
```sql
-- Check if migration was applied
SELECT * FROM [__EFMigrationsHistory] 
WHERE MigrationId = '20260414202855_AddSuggestionsFeature'
```

If it shows:
- ❌ NOT in list = Migration never applied (will try again on startup)
- ✅ In list = Migration already applied

---

## 🚨 **Step 4: Common Causes & Solutions**

### **Cause #1: Schema Creation Failures**
We added better error handling in the latest commit. This prevents crashes from columns already existing.

**Updated Code:**
```csharp
try
{
    EnsureMemberSecuritySchema(dbContext);
    startupLogger.LogInformation("✅ Schema ensured");
}
catch (Exception ex)
{
    startupLogger.LogWarning(ex, "⚠️ Schema creation failed (may already exist)");
}
```

✅ **Deploy the latest version** to get this fix.

### **Cause #2: Connection Pool Exhaustion**
Rapid restarts can exhaust connection pools. 
- Solution: Wait 2 minutes between restarts
- Stop the app pool, wait, then start it again

### **Cause #3: JWT Configuration Missing**
The app requires JWT settings in `appsettings.json`:
```json
{
  "Jwt": {
    "Key": "your-very-long-secret-key-at-least-32-characters",
    "Issuer": "basmet-shabab",
    "Audience": "basmet-shabab-client"
  }
}
```

**Check if set:**
- ❌ Missing = 401 errors + crashes
- ✅ Present = Should work

### **Cause #4: Suggestions Feature Not Migrated**
If your database schema is old:

**Manually run this on SQL Server:**
```sql
-- Check if Suggestions table exists
SELECT OBJECT_ID('dbo.Suggestions') AS TableId;

-- If NULL, the migration didn't apply
-- You need to manually run the migration
```

---

## 🚀 **Step 5: Deploy the Fix**

### **Option A: Pull Latest Changes**
```bash
git pull origin main
dotnet build -c Release
# Then republish to runasp.net
```

This includes:
- ✅ Better error handling for schema initialization 
- ✅ Safer startup process
- ✅ Suggestions feature properly configured

### **Option B: Publish to runasp.net**

1. **Build Release:**
```bash
cd e:\basma\BasmaApi
dotnet publish -c Release -o .\publish
```

2. **Upload to runasp.net:**
   - Open runasp.net Control Panel
   - Click **Publish Website** or **File Manager**
   - Upload contents of `.\publish` folder
   - Replace existing files

3. **Test URL:**
```
https://basmet-shabab.runasp.net/swagger/
```

---

## ✅ **Step 6: Verify It's Fixed**

After deploying:

### **Test Endpoints**
```bash
# Test API is running
curl https://basmet-shabab.runasp.net/swagger/

# Test login endpoint
curl -X POST https://basmet-shabab.runasp.net/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"president@basmet.local","password":"123"}'
```

### **Check Logs Again**
Go back to runasp.net Event Logs and verify no new errors appear.

### **Test User Actions**
1. Log in with: `president@basmet.local` / `123`
2. Check dashboard loads
3. Try creating a task
4. Test suggestions feature (if applicable)

---

## 📧 **Still Having Issues?**

### **Gather This Information:**

1. **Full error message from Event Logs** (copy entire stack trace)
2. **Connection string** (hide password): `Server=XXX;Database=XXX;User=XXX;Password=***`
3. **Current .NET version:** 
   ```bash
   dotnet --version  # Should be .NET 10
   ```
4. **Deployment method** (FTP, Control Panel, etc.)
5. **Last successful deploy** (date/time)

---

## 🔗 **Useful Commands**

```bash
# Check current version deployed
curl https://basmet-shabab.runasp.net/api/health

# View logs in production
# (Goes to runasp.net dashboard → Event Logs)

# Build locally to test
cd e:\basma\BasmaApi && dotnet build -c Release

# Test locally before deploying
dotnet run --configuration Release
# Then visit: https://localhost:5001/swagger/
```

---

## 📋 **Deployment Checklist**

- [ ] Latest code pulled (`git pull origin main`)
- [ ] Backend builds: `dotnet build -c Release` ✅ 
- [ ] appsettings.json has correct connection string
- [ ] JWT keys are set
- [ ] Frontend deployed and working
- [ ] Database backups created
- [ ] Old app pool stopped before deploying
- [ ] New version deployed to runasp.net
- [ ] Event logs checked (no new errors)
- [ ] Swagger endpoint responds: `/swagger/`
- [ ] Login test succeeds
- [ ] Dashboard loads

---

**Last Updated**: April 14, 2026  
**Status**: ✅ Diagnostics Complete - Ready for Deployment

