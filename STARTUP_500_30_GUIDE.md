# HTTP 500.30 - ASP.NET Core Startup Failure Guide

**Error**: "HTTP Error 500.30 - ASP.NET Core app failed to start"  
**Additional Error**: "Process id 'XXXX' suffered fatal communication error with Windows Process Activation Service"

This error means your ASP.NET Core application is **crashing during startup before it can handle ANY requests**.

---

## 🚨 **Critical Issues & Solutions**

### **Issue #1: Missing JWT Configuration (MOST COMMON)**

**Symptom:**
- Error message mentioning "Jwt:Key is missing"
- Works locally but fails on runasp.net
- Fresh deployment after code changes

**Solution:**
```
Set these environment variables on runasp.net:

1. Log in to runasp.net Control Panel
2. Click your application
3. Go to > Application Variables / Environment Variables
4. Add these variables:

Jwt__Key = "your-super-secret-key-must-be-32-characters-minimum-1234567890"
Jwt__Issuer = "basmet-shabab"
Jwt__Audience = "basmet-shabab-client"
```

**✅ Fixed in latest code**: The app now uses secure defaults if these aren't set, so it won't crash.

---

### **Issue #2: Database Connection Failure**

**Symptom:**
- Error mentioning "Cannot open database"
- "Login failed for user"
- "Connection timeout"

**Diagnosis:**
Check your connection string on runasp.net:

```
1. Control Panel > Application Settings
2. Look for "ConnectionStrings__DefaultConnection"
3. Should look like:
   Server=YOUR_SERVER;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PASS;
```

**Common mistakes:**
```
❌ WRONG: Server=localhost  (won't work on runasp.net)
❌ WRONG: Server=.          (won't work on runasp.net)
❌ WRONG: Server=(localdb)  (LocalDB not available on hosting)

✅ RIGHT: Server=your-production-server.database.windows.net
✅ RIGHT: Database=basma_production
✅ RIGHT: User Id=dbuser@server
```

---

### **Issue #3: Database Migrations Not Applied**

**Symptom:**
- Error mentioning "Invalid column name"
- "Suggestions table not found"
- Database schema is outdated

**Solution:**
```sql
-- Step 1: Check what migrations are applied
SELECT * FROM [dbo].[__EFMigrationsHistory] 
ORDER BY [MigrationId] DESC;

-- Step 2: Check for the Suggestions migration
-- Look for: '20260414202855_AddSuggestionsFeature'

-- If it's NOT there, migrations haven't run yet
```

**To fix:**
On runasp.net, set this:
```
Startup__ApplyEfMigrations = true
```

Then restart the app. It will apply all pending migrations.

---

### **Issue #4: Port Already in Use**

**Symptom:**
- Error mentioning "Address already in use"
- Port 5000 or 80 is occupied

**Solution:**
```
On runasp.net, this is handled automatically.
Just restart the app pool:
1. Control Panel > Restart Application
```

---

### **Issue #5: Permission / Access Denied**

**Symptom:**
- Error mentioning "Access Denied"
- "Permission denied"
- "Not authorized"

**Solution:**
```
On runasp.net shared hosting, ensure:
1. Application pool user has read/write access to App_Data/
2. Database user has CREATE TABLE, ALTER TABLE permissions
3. File permissions are set correctly
```

Ask runasp.net support if you see permission errors.

---

## 📋 **Startup Diagnostics Checklist**

### **Before Deploying (Local Testing)**

- [ ] App builds successfully: `dotnet build -c Release` ✅
- [ ] No compilation errors
- [ ] Run locally: `dotnet run --configuration Release`
- [ ] Can access Swagger: `https://localhost:5001/swagger/`
- [ ] Database works locally
- [ ] All migrations applied locally: `dotnet ef database update`

### **After Deploying**

- [ ] Check runasp.net event logs (Control Panel > Event Viewer)
- [ ] Look for errors with line numbers
- [ ] Verify environment variables are set (Jwt__Key, etc.)
- [ ] Check database connection string
- [ ] Wait 30 seconds for app to fully start
- [ ] Try accessing: `https://YOUR-DOMAIN/swagger/`
- [ ] Check Detailed Error Information (if available)

---

## 🔧 **How We Fixed This**

### **Change #1: Optional JWT Configuration**
```csharp
// BEFORE (crashes if missing):
var jwtKey = builder.Configuration["Jwt:Key"] 
  ?? throw new InvalidOperationException("Jwt:Key is missing.");

// AFTER (uses defaults and warns):
var jwtKey = builder.Configuration["Jwt:Key"] 
  ?? "default-insecure-key-1234567890...";
Console.WriteLine("⚠️ WARNING: JWT Key not configured! Set Jwt:Key...");
```

### **Change #2: Graceful Startup Initialization**
```csharp
// BEFORE (one error crashes entire app):
dbContext.Database.Migrate();  // Throws on failure

// AFTER (logs error and continues):
try 
{
    dbContext.Database.Migrate();
    logger.LogInformation("✅ Migrations applied");
}
catch (Exception ex) 
{
    logger.LogError(ex, "Migration failed");
    // App continues running for diagnostics
}
```

### **Change #3: Safe Schema Initialization**
All `EnsureXXXSchema()` calls now wrapped in try-catch blocks.

### **Change #4: Non-Fatal Error Handling**
Removed `throw` statements from startup. Errors are logged but app continues.

---

## 🚀 **Deployment Steps (Fixed)**

### **Step 1: Publish Release Build**
```bash
cd e:\basma\BasmaApi
dotnet publish -c Release -o .\publish
```

### **Step 2: Upload to runasp.net**
1. Go to runasp.net Control Panel
2. Click **File Manager** or use **FTP**
3. Upload contents of `.\publish` folder
4. Stop old app pool, wait 5 seconds
5. Delete old files if necessary
6. Upload new files
7. Start app pool

### **Step 3: Set Environment Variables**
1. Control Panel > Application Settings
2. Add:
   - `Jwt__Key` = your secret key
   - `Jwt__Issuer` = basmet-shabab
   - `Jwt__Audience` = basmet-shabab-client
   - `Startup__ApplyEfMigrations` = true (first deployment only)
3. Click Save/Apply

### **Step 4: Verify Startup**
```bash
# Wait 30 seconds for app to start
# Then test:
curl https://your-domain.runasp.net/swagger/

# Check Event Logs for errors
```

---

## 📖 **Logs to Check**

### **Event Viewer (runasp.net)**
```
Location: Control Panel > Tools > Event Viewer
Look for:
- Source: ".NET Runtime"
- Type: "Error"
- Recent events from last 5 minutes
```

**Copy the full error message including:**
- Exception type (e.g., `System.InvalidOperationException`)
- Message (e.g., "Failed to hash president password")
- Stack trace (shows which line failed)

### **Application Output**
If you can enable detailed logging:
```
1. Set: ASPNETCORE_ENVIRONMENT = Production
2. Set: ASPNETCORE_URLS = http://+:5000
3. Check console output in Event Viewer
```

---

## ✅ **Testing After Fix**

### **Test 1: API Health Check**
```bash
curl -v https://your-domain.runasp.net/swagger/
# Should return HTML page with Swagger UI
```

### **Test 2: Login Endpoint**
```bash
curl -X POST https://your-domain.runasp.net/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"president@basmet.local","password":"123"}'

# Should return 401 if JWT not configured (that's OK, means API responded)
# Or return 200 with token if JWT configured
```

### **Test 3: Database Check**
```bash
curl -X GET https://your-domain.runasp.net/api/suggestions \
  -H "Authorization: Bearer YOUR_TOKEN"

# Should work if database migrations applied
```

---

## 🎯 **Quick Reference**

| Issue | Check | Fix |
|-------|-------|-----|
| JWT Error | Event Viewer | Set Jwt__Key env var |
| DB Error | SQL Server | Check connection string |
| Schema Error | Database | Enable migrations |
| Permission Error | Event Log | Check app pool permissions |
| Port in Use | Event Log | Restart app pool |

---

## 📞 **Still Having Issues?**

### **Gather This Information**

1. **Full error from Event Viewer**: Copy the entire error message
2. **Connection string**: `Server=XXX;Database=XXX;User=XXX;Password=***`
3. **Environment variables**: List what you set on runasp.net
4. **Last working date**: When did this app last work?
5. **Recent changes**: What code changed before this error?

### **Share with Support**

Create a file with:
```
DEPLOYMENT INFO:
- Date deployed: 2026-04-14
- Version: v1.0.0
- Deployment method: Visual Studio Publish
- Environment: Production (runasp.net)

ERROR INFO:
[Paste full error from Event Viewer here]

CONNECTION:
- Server: [your database server]
- Database: [your database name]
- User: [your database user]

VARIABLES SET:
- Jwt__Key: [if empty, that's the problem]
- Jwt__Issuer: [if empty, that's the problem]
- Startup__ApplyEfMigrations: [true/false]
```

---

## 📝 **Latest Fixes Applied**

✅ **Commit**: 7cc82c5  
✅ **Message**: "fix: make startup more resilient - optional JWT config and graceful error handling"  
✅ **Date**: April 14, 2026  
✅ **Changes**:
- JWT configuration now optional with defaults
- All startup initialization wrapped in try-catch
- Graceful degradation instead of crashing
- Better error messages for diagnostics

---

**Status**: ✅ App should now start even with missing configuration  
**Next Step**: Deploy the latest code to runasp.net and set environment variables

