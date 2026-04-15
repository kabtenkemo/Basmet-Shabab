using System.Text;
using System.Threading.RateLimiting;
using BasmaApi.Data;
using BasmaApi.Middleware;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "منصة بصمة شباب API",
        Version = "v1",
        Description = "API لتسجيل الدخول وإدارة مهام الأعضاء"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "أدخل التوكن بصيغة: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuditRequestContextAccessor, AuditRequestContextAccessor>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IComplaintEscalationService, ComplaintEscalationService>();
builder.Services.AddHostedService<ComplaintEscalationWorker>();

// FIX: CORS hardened - limit to specific methods and headers
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "https://basmet-shabab.netlify.app")
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Content-Type", "Authorization")
            .AllowCredentials();
    });
});

builder.Services.AddScoped<IPasswordService, BcryptPasswordService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();

// FIX: Add Rate Limiting for login and auth endpoints
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // FIX: Extract real client IP when behind proxy (Netlify, Cloudflare, etc.)
        var remoteIp = GetClientIpAddress(context);
        
        // Strict limits for authentication endpoints
        if (context.Request.Path.StartsWithSegments("/api/auth"))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: remoteIp,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(15),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
        }

        // Standard limits for other endpoints
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: remoteIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });
    
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            message = "تم تجاوز حد الطلبات. يرجى المحاولة لاحقاً."
        }, cancellationToken);
    };
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// JWT configuration - get from configuration
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

// Use secure defaults for development if not configured
jwtKey ??= "dev-key-1234567890123456789012345678901234567890";
jwtIssuer ??= "basmet-shabab-dev";
jwtAudience ??= "basmet-shabab-client-dev";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "يجب تسجيل الدخول مرة أخرى." });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "ليس لديك صلاحية لتنفيذ هذا الطلب." });
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// FIX: Require JWT configuration in production
if (app.Environment.IsProduction())
{
    if (string.IsNullOrEmpty(builder.Configuration["Jwt:Key"]))
        throw new InvalidOperationException("CRITICAL: Jwt:Key must be configured in production. Set environment variable Jwt__Key");
    if (string.IsNullOrEmpty(builder.Configuration["Jwt:Issuer"]))
        throw new InvalidOperationException("CRITICAL: Jwt:Issuer must be configured in production.");
    if (string.IsNullOrEmpty(builder.Configuration["Jwt:Audience"]))
        throw new InvalidOperationException("CRITICAL: Jwt:Audience must be configured in production.");
}

if (!app.Environment.IsProduction())
{
    Console.WriteLine("⚠️  Running in Development mode with default JWT keys. Configure for production.");
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StartupInitialization");

    try
    {
        startupLogger.LogInformation("🚀 Starting database initialization...");
        
        if (app.Environment.IsProduction())
        {
            var currentConnection = dbContext.Database.GetConnectionString() ?? string.Empty;
            if (currentConnection.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is still pointing to LocalDB in Production.");
            }
        }

        try
        {
            var applyEfMigrations = app.Configuration.GetValue<bool>("Startup:ApplyEfMigrations");
            if (app.Environment.IsDevelopment() || applyEfMigrations)
            {
                startupLogger.LogInformation("📊 Applying EF Core migrations...");
                dbContext.Database.Migrate();
                startupLogger.LogInformation("✅ Migrations applied successfully");
            }
            else
            {
                startupLogger.LogWarning("⏭️  Skipping EF Core migrations on startup in Production. Set Startup:ApplyEfMigrations=true to force migration execution.");
            }
        }
        catch (Exception migrationEx)
        {
            startupLogger.LogError(migrationEx, "❌ Migration failed. App will continue without DB schema updates.");
            // Don't throw - let app start anyway for diagnostics
        }

        try
        {
            EnsureMemberIdentityColumns(dbContext);
            startupLogger.LogInformation("✅ Member identity columns ensured");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "⚠️ Failed to ensure member identity columns (may already exist)");
        }

        try
        {
            EnsureMemberSecuritySchema(dbContext);
            startupLogger.LogInformation("✅ Member security schema ensured");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "⚠️ Failed to ensure member security schema (may already exist)");
        }

        try
        {
            EnsureComplaintAuditSchema(dbContext);
            startupLogger.LogInformation("✅ Complaint audit schema ensured");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "⚠️ Failed to ensure complaint audit schema (may already exist)");
        }

        try
        {
            EnsureReferenceDataSchema(dbContext);
            startupLogger.LogInformation("✅ Reference data schema ensured");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "⚠️ Failed to ensure reference data schema (may already exist)");
        }

        try
        {
            EnsureTaskSchema(dbContext);
            startupLogger.LogInformation("✅ Task schema ensured");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "⚠️ Failed to ensure task schema (may already exist)");
        }

        try
        {
            EnsureNewsSchema(dbContext);
            startupLogger.LogInformation("✅ News schema ensured");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "⚠️ Failed to ensure news schema (may already exist)");
        }

        try
        {
            SeedReferenceData(dbContext);
            startupLogger.LogInformation("✅ Reference data seeded");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "⚠️ Failed to seed reference data (may already exist)");
        }

        // Initialize President account (safe version)
        try
        {
            var targetPresidentEmail = "president@basmet.local";
            var targetPresidentPassword = "Test123.";
            var president = dbContext.Members.FirstOrDefault(member => member.Role == MemberRole.President);
            var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

            // FIX: Only update President account if fields have actually changed
            // This prevents unnecessary database writes and email rewriting on every startup
            if (president is null)
            {
                var hashedPassword = passwordService.HashPassword(targetPresidentPassword);
                
                if (string.IsNullOrWhiteSpace(hashedPassword))
                {
                    throw new InvalidOperationException("Failed to hash president password!");
                }

                president = new Member
                {
                    FullName = "رئيس الكيان",
                    Email = targetPresidentEmail.ToLowerInvariant(),
                    NationalId = "00000000000001",
                    BirthDate = new DateOnly(1980, 1, 1),
                    Role = MemberRole.President,
                    Points = 0,
                    MustChangePassword = false,
                    PasswordHash = hashedPassword
                };

                dbContext.Members.Add(president);
                dbContext.SaveChanges();
                startupLogger.LogInformation("✅ Created new President account: Email={Email}, PasswordHashLength={HashLength}", 
                    targetPresidentEmail.ToLowerInvariant(), 
                    hashedPassword.Length);
            }
            else
            {
                // Only update if needed
                bool needsUpdate = false;

                // Check if email needs updating
                if (!string.Equals(president.Email, targetPresidentEmail, StringComparison.OrdinalIgnoreCase))
                {
                    president.Email = targetPresidentEmail.ToLowerInvariant();
                    needsUpdate = true;
                    startupLogger.LogInformation("🔄 President email updated: {OldEmail} → {NewEmail}", 
                        "****", targetPresidentEmail.ToLowerInvariant());
                }

                // Only update missing fields
                if (string.IsNullOrWhiteSpace(president.FullName))
                {
                    president.FullName = "رئيس الكيان";
                    needsUpdate = true;
                }

                if (string.IsNullOrWhiteSpace(president.NationalId))
                {
                    president.NationalId = "00000000000001";
                    needsUpdate = true;
                }

                if (president.BirthDate is null)
                {
                    president.BirthDate = new DateOnly(1980, 1, 1);
                    needsUpdate = true;
                }

                // Check if password needs updating (only if hash is invalid or missing)
                if (string.IsNullOrWhiteSpace(president.PasswordHash))
                {
                    var newHash = passwordService.HashPassword(targetPresidentPassword);
                    if (string.IsNullOrWhiteSpace(newHash))
                    {
                        throw new InvalidOperationException("Failed to hash president password!");
                    }
                    president.PasswordHash = newHash;
                    needsUpdate = true;
                    startupLogger.LogInformation("🔐 President password hash was missing, now set");
                }

                if (president.MustChangePassword != false)
                {
                    president.MustChangePassword = false;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    dbContext.SaveChanges();
                    startupLogger.LogInformation("✅ Updated President account (only changed fields)");
                }
                else
                {
                    startupLogger.LogInformation("ℹ️ President account already configured correctly - skipping update");
                }
            }
        }
        catch (Exception presidentialEx)
        {
            startupLogger.LogError(presidentialEx, "❌ Failed to initialize President account. Continuing startup...");
            // Don't throw - let app start so we can diagnose
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "❌ Critical error during startup initialization.");
        startupLogger.LogWarning("⚠️ Application is starting despite initialization errors. Some features may be unavailable.");
        // Don't throw - allow app to start for diagnostics
    }
}

static void EnsureMemberIdentityColumns(AppDbContext dbContext)
{
    dbContext.Database.ExecuteSqlRaw("IF COL_LENGTH('dbo.Members', 'NationalId') IS NULL ALTER TABLE dbo.Members ADD NationalId nvarchar(14) NULL;");
    dbContext.Database.ExecuteSqlRaw("IF COL_LENGTH('dbo.Members', 'BirthDate') IS NULL ALTER TABLE dbo.Members ADD BirthDate date NULL;");
}

static void EnsureMemberSecuritySchema(AppDbContext dbContext)
{
    dbContext.Database.ExecuteSqlRaw("IF COL_LENGTH('dbo.Members', 'MustChangePassword') IS NULL ALTER TABLE dbo.Members ADD MustChangePassword bit NOT NULL CONSTRAINT DF_Members_MustChangePassword DEFAULT 0;");
}

static void EnsureComplaintAuditSchema(AppDbContext dbContext)
{
    dbContext.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('dbo.Complaints', 'Priority') IS NULL ALTER TABLE dbo.Complaints ADD Priority nvarchar(20) NOT NULL CONSTRAINT DF_Complaints_Priority DEFAULT 'Medium';
IF COL_LENGTH('dbo.Complaints', 'EscalationLevel') IS NULL ALTER TABLE dbo.Complaints ADD EscalationLevel int NOT NULL CONSTRAINT DF_Complaints_EscalationLevel DEFAULT 0;
IF COL_LENGTH('dbo.Complaints', 'LastActionDate') IS NULL ALTER TABLE dbo.Complaints ADD LastActionDate datetime2 NOT NULL CONSTRAINT DF_Complaints_LastActionDate DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('dbo.Complaints', 'AssignedToMemberId') IS NULL ALTER TABLE dbo.Complaints ADD AssignedToMemberId uniqueidentifier NULL;

IF OBJECT_ID('dbo.ComplaintHistories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ComplaintHistories (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_ComplaintHistories PRIMARY KEY,
        ComplaintId uniqueidentifier NOT NULL,
        Action nvarchar(30) NOT NULL,
        PerformedByUserId uniqueidentifier NULL,
        Notes nvarchar(4000) NULL,
        [Timestamp] datetime2 NOT NULL,
        CONSTRAINT FK_ComplaintHistories_Complaints_ComplaintId FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints (Id) ON DELETE CASCADE,
        CONSTRAINT FK_ComplaintHistories_Members_PerformedByUserId FOREIGN KEY (PerformedByUserId) REFERENCES dbo.Members (Id) ON DELETE NO ACTION
    );
END;

IF OBJECT_ID('dbo.AuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
        UserId uniqueidentifier NULL,
        UserName nvarchar(150) NOT NULL,
        ActionType nvarchar(50) NOT NULL,
        EntityName nvarchar(80) NOT NULL,
        EntityId nvarchar(100) NULL,
        OldValuesJson nvarchar(max) NULL,
        NewValuesJson nvarchar(max) NULL,
        TimestampUtc datetime2 NOT NULL,
        IPAddress nvarchar(45) NULL
    );
END;
" );
}

static void EnsureReferenceDataSchema(AppDbContext dbContext)
{
    dbContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID('dbo.Governorates', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Governorates (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Governorates PRIMARY KEY,
        Name nvarchar(120) NOT NULL
    );
END;

IF OBJECT_ID('dbo.Committees', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Committees (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Committees PRIMARY KEY,
        GovernorateId uniqueidentifier NOT NULL,
        Name nvarchar(120) NOT NULL,
        CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_Committees_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Committees_Governorates_GovernorateId FOREIGN KEY (GovernorateId) REFERENCES dbo.Governorates (Id) ON DELETE CASCADE
    );
END;

IF COL_LENGTH('dbo.Members', 'GovernorateId') IS NULL ALTER TABLE dbo.Members ADD GovernorateId uniqueidentifier NULL;
IF COL_LENGTH('dbo.Members', 'CommitteeId') IS NULL ALTER TABLE dbo.Members ADD CommitteeId uniqueidentifier NULL;
IF COL_LENGTH('dbo.Members', 'GovernorName') IS NULL ALTER TABLE dbo.Members ADD GovernorName nvarchar(120) NULL;
IF COL_LENGTH('dbo.Members', 'CommitteeName') IS NULL ALTER TABLE dbo.Members ADD CommitteeName nvarchar(120) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Governorates_Name' AND object_id = OBJECT_ID('dbo.Governorates'))
BEGIN
    CREATE UNIQUE INDEX IX_Governorates_Name ON dbo.Governorates (Name);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Committees_GovernorateId_Name' AND object_id = OBJECT_ID('dbo.Committees'))
BEGIN
    CREATE UNIQUE INDEX IX_Committees_GovernorateId_Name ON dbo.Committees (GovernorateId, Name);
END;
" );
}

static void EnsureTaskSchema(AppDbContext dbContext)
{
    dbContext.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('dbo.Tasks', 'AudienceType') IS NULL ALTER TABLE dbo.Tasks ADD AudienceType nvarchar(20) NOT NULL CONSTRAINT DF_Tasks_AudienceType DEFAULT 'All';

IF OBJECT_ID('dbo.TaskTargetRoles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaskTargetRoles (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_TaskTargetRoles PRIMARY KEY,
        TaskId uniqueidentifier NOT NULL,
        Role nvarchar(40) NOT NULL,
        CONSTRAINT FK_TaskTargetRoles_Tasks_TaskId FOREIGN KEY (TaskId) REFERENCES dbo.Tasks (Id) ON DELETE CASCADE
    );
END;

IF OBJECT_ID('dbo.TaskTargetMembers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaskTargetMembers (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_TaskTargetMembers PRIMARY KEY,
        TaskId uniqueidentifier NOT NULL,
        MemberId uniqueidentifier NOT NULL,
        CONSTRAINT FK_TaskTargetMembers_Tasks_TaskId FOREIGN KEY (TaskId) REFERENCES dbo.Tasks (Id) ON DELETE CASCADE,
        CONSTRAINT FK_TaskTargetMembers_Members_MemberId FOREIGN KEY (MemberId) REFERENCES dbo.Members (Id) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TaskTargetRoles_TaskId_Role' AND object_id = OBJECT_ID('dbo.TaskTargetRoles'))
BEGIN
    CREATE UNIQUE INDEX IX_TaskTargetRoles_TaskId_Role ON dbo.TaskTargetRoles (TaskId, Role);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TaskTargetMembers_TaskId_MemberId' AND object_id = OBJECT_ID('dbo.TaskTargetMembers'))
BEGIN
    CREATE UNIQUE INDEX IX_TaskTargetMembers_TaskId_MemberId ON dbo.TaskTargetMembers (TaskId, MemberId);
END;
" );
}

static void EnsureNewsSchema(AppDbContext dbContext)
{
    dbContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID('dbo.NewsPosts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NewsPosts (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_NewsPosts PRIMARY KEY,
        Title nvarchar(250) NOT NULL,
        Content nvarchar(4000) NOT NULL,
        CreatedByMemberId uniqueidentifier NOT NULL,
        AudienceType nvarchar(20) NOT NULL,
        CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_NewsPosts_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_NewsPosts_Members_CreatedByMemberId FOREIGN KEY (CreatedByMemberId) REFERENCES dbo.Members (Id) ON DELETE NO ACTION
    );
END;

IF OBJECT_ID('dbo.NewsTargetRoles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NewsTargetRoles (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_NewsTargetRoles PRIMARY KEY,
        NewsPostId uniqueidentifier NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        CONSTRAINT FK_NewsTargetRoles_NewsPosts_NewsPostId FOREIGN KEY (NewsPostId) REFERENCES dbo.NewsPosts (Id) ON DELETE CASCADE
    );
END;

IF OBJECT_ID('dbo.NewsTargetMembers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NewsTargetMembers (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_NewsTargetMembers PRIMARY KEY,
        NewsPostId uniqueidentifier NOT NULL,
        MemberId uniqueidentifier NOT NULL,
        CONSTRAINT FK_NewsTargetMembers_NewsPosts_NewsPostId FOREIGN KEY (NewsPostId) REFERENCES dbo.NewsPosts (Id) ON DELETE CASCADE,
        CONSTRAINT FK_NewsTargetMembers_Members_MemberId FOREIGN KEY (MemberId) REFERENCES dbo.Members (Id) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NewsTargetRoles_NewsPostId_Role' AND object_id = OBJECT_ID('dbo.NewsTargetRoles'))
BEGIN
    CREATE UNIQUE INDEX IX_NewsTargetRoles_NewsPostId_Role ON dbo.NewsTargetRoles (NewsPostId, [Role]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NewsTargetMembers_NewsPostId_MemberId' AND object_id = OBJECT_ID('dbo.NewsTargetMembers'))
BEGIN
    CREATE UNIQUE INDEX IX_NewsTargetMembers_NewsPostId_MemberId ON dbo.NewsTargetMembers (NewsPostId, MemberId);
END;
" );
}

static void SeedReferenceData(AppDbContext dbContext)
{
    if (!dbContext.Governorates.Any())
    {
        var governorates = new[]
        {
            "القاهرة",
            "الإسكندرية",
            "الجيزة",
            "القليوبية",
            "الشرقية",
            "الغربية",
            "المنوفية",
            "الدقهلية",
            "البحيرة",
            "كفر الشيخ",
            "دمياط",
            "بورسعيد",
            "السويس",
            "الإسماعيلية",
            "شمال سيناء",
            "جنوب سيناء",
            "الفيوم",
            "بني سويف",
            "المنيا",
            "أسيوط",
            "سوهاج",
            "قنا",
            "الأقصر",
            "أسوان",
            "البحر الأحمر",
            "الوادي الجديد",
            "مطروح"
        };

        foreach (var name in governorates)
        {
            dbContext.Governorates.Add(new Governorate { Name = name });
        }

        dbContext.SaveChanges();
    }

    if (!dbContext.Committees.Any())
    {
        var committeeTemplates = new[]
        {
            "لجنة التنظيم",
            "لجنة الشباب",
            "لجنة الخدمات"
        };

        var governorates = dbContext.Governorates.AsNoTracking().OrderBy(item => item.Name).ToList();
        foreach (var governorate in governorates)
        {
            foreach (var committeeTemplate in committeeTemplates)
            {
                dbContext.Committees.Add(new Committee
                {
                    GovernorateId = governorate.Id,
                    Name = $"{committeeTemplate} - {governorate.Name}"
                });
            }
        }

        dbContext.SaveChanges();
    }
}

// Global exception handling
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        
        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;
        
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception occurred");
        
        await context.Response.WriteAsJsonAsync(new
        {
            message = "حدث خطأ في الخادم. يرجى محاولة مرة أخرى لاحقاً.",
            error = app.Environment.IsDevelopment() ? exception?.Message : null
        });
    });
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("ClientApp");

app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<PasswordChangeRequiredMiddleware>();
app.UseMiddleware<AuditRequestContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Helper function to get real client IP when behind proxy
static string GetClientIpAddress(HttpContext context)
{
    // Check X-Forwarded-For header (used by proxies like Netlify, Cloudflare, Nginx, etc.)
    if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
    {
        var ips = forwardedFor.ToString().Split(',');
        if (ips.Length > 0 && !string.IsNullOrWhiteSpace(ips[0]))
        {
            return ips[0].Trim();
        }
    }

    // Check CF-Connecting-IP header (Cloudflare)
    if (context.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfConnectingIp))
    {
        var ip = cfConnectingIp.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(ip))
        {
            return ip;
        }
    }

    // Check X-Real-IP header (Nginx and others)
    if (context.Request.Headers.TryGetValue("X-Real-IP", out var xRealIp))
    {
        var ip = xRealIp.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(ip))
        {
            return ip;
        }
    }

    // Fallback to direct connection IP
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
