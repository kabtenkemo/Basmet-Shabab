using System.Text;
using System.Diagnostics;
using BasmaApi.Data;
using BasmaApi.Middleware;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
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
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        sqlOptions.CommandTimeout(30);
    });
    options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuditRequestContextAccessor, AuditRequestContextAccessor>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IComplaintEscalationService, ComplaintEscalationService>();
builder.Services.AddHostedService<ComplaintEscalationWorker>();
builder.Services.AddHostedService<AuditLogCleanupWorker>();

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

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
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

if (!app.Environment.IsProduction() && string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]))
{
    app.Logger.LogWarning("Running with development JWT fallback key. Configure Jwt:Key before deploying shared environments.");
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StartupInitialization");

    try
    {
        startupLogger.LogInformation("Startup initialization started.");
        
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
                startupLogger.LogInformation("Applying EF Core migrations.");
                dbContext.Database.Migrate();
                startupLogger.LogInformation("EF Core migrations applied successfully.");
            }
            else
            {
                startupLogger.LogWarning("Skipping EF Core migrations on startup in Production. Set Startup:ApplyEfMigrations=true to force migration execution.");
            }
        }
        catch (Exception migrationEx)
        {
            startupLogger.LogError(migrationEx, "EF Core migration failed. App will continue without DB schema updates.");
            // Don't throw - let app start anyway for diagnostics
        }

        try
        {
            EnsureMemberIdentityColumns(dbContext);
            startupLogger.LogInformation("Member identity columns ensured.");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "Failed to ensure member identity columns (may already exist).");
        }

        try
        {
            EnsureMemberSecuritySchema(dbContext);
            startupLogger.LogInformation("Member security schema ensured.");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "Failed to ensure member security schema (may already exist).");
        }

        try
        {
            EnsureComplaintAuditSchema(dbContext);
            startupLogger.LogInformation("Complaint audit schema ensured.");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "Failed to ensure complaint audit schema (may already exist).");
        }

        try
        {
            EnsureReferenceDataSchema(dbContext);
            startupLogger.LogInformation("Reference data schema ensured.");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "Failed to ensure reference data schema (may already exist).");
        }

        try
        {
            EnsureTaskSchema(dbContext);
            startupLogger.LogInformation("Task schema ensured.");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "Failed to ensure task schema (may already exist).");
        }

        try
        {
            EnsureNewsSchema(dbContext);
            startupLogger.LogInformation("News schema ensured.");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "Failed to ensure news schema (may already exist).");
        }

        try
        {
            EnsureJoinRequestsSchema(dbContext);
            startupLogger.LogInformation("Join request schema ensured.");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "Failed to ensure join request schema (may already exist).");
        }

        try
        {
            SeedReferenceData(dbContext);
            startupLogger.LogInformation("Reference data seeded.");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "Failed to seed reference data (may already exist).");
        }

        // Initialize President account (safe version)
        try
        {
            var shouldSeedPresident = app.Environment.IsDevelopment()
                || app.Configuration.GetValue<bool>("Startup:SeedPresidentInProduction");

            if (!shouldSeedPresident)
            {
                startupLogger.LogInformation("President bootstrap skipped. Set Startup:SeedPresidentInProduction=true to enable outside Development.");
            }
            else
            {
                var configuredEmail = app.Configuration["BootstrapAdmin:Email"];
                var targetPresidentEmail = string.IsNullOrWhiteSpace(configuredEmail)
                    ? "president@basmet.local"
                    : configuredEmail.Trim().ToLowerInvariant();

                var configuredPassword = app.Configuration["BootstrapAdmin:Password"];
                var targetPresidentPassword = string.IsNullOrWhiteSpace(configuredPassword)
                    ? (app.Environment.IsDevelopment() ? "Test123" : null)
                    : configuredPassword;

                var president = dbContext.Members.FirstOrDefault(member => member.Role == MemberRole.President);
                var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

                if (president is null)
                {
                    if (string.IsNullOrWhiteSpace(targetPresidentPassword))
                    {
                        startupLogger.LogWarning("President bootstrap skipped because BootstrapAdmin:Password is missing in non-development environment.");
                    }
                    else
                    {
                        var hashedPassword = passwordService.HashPassword(targetPresidentPassword);

                        if (string.IsNullOrWhiteSpace(hashedPassword))
                        {
                            throw new InvalidOperationException("Failed to hash president password.");
                        }

                        president = new Member
                        {
                            FullName = "رئيس الكيان",
                            Email = targetPresidentEmail,
                            NationalId = "00000000000001",
                            BirthDate = new DateOnly(1980, 1, 1),
                            Role = MemberRole.President,
                            Points = 0,
                            MustChangePassword = false,
                            PasswordHash = hashedPassword
                        };

                        dbContext.Members.Add(president);
                        dbContext.SaveChanges();
                        startupLogger.LogInformation("Created President account. Email={Email}", targetPresidentEmail);
                    }
                }
                else
                {
                    bool needsUpdate = false;

                    if (!string.Equals(president.Email, targetPresidentEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        president.Email = targetPresidentEmail;
                        needsUpdate = true;
                        startupLogger.LogInformation("President email normalized to configured value.");
                    }

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

                    if (string.IsNullOrWhiteSpace(president.PasswordHash))
                    {
                        if (string.IsNullOrWhiteSpace(targetPresidentPassword))
                        {
                            startupLogger.LogWarning("President password hash is missing and BootstrapAdmin:Password is not configured. Password hash was not updated.");
                        }
                        else
                        {
                            var newHash = passwordService.HashPassword(targetPresidentPassword);
                            if (string.IsNullOrWhiteSpace(newHash))
                            {
                                throw new InvalidOperationException("Failed to hash president password.");
                            }

                            president.PasswordHash = newHash;
                            needsUpdate = true;
                            startupLogger.LogInformation("President password hash was initialized.");
                        }
                    }

                    if (president.MustChangePassword != false)
                    {
                        president.MustChangePassword = false;
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        dbContext.SaveChanges();
                        startupLogger.LogInformation("Updated President account fields that required changes.");
                    }
                    else
                    {
                        startupLogger.LogInformation("President account is already configured correctly.");
                    }
                }
            }
        }
        catch (Exception presidentialEx)
        {
            startupLogger.LogError(presidentialEx, "Failed to initialize President account. Continuing startup.");
            // Don't throw - let app start so we can diagnose
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Critical error during startup initialization.");
        startupLogger.LogWarning("Application is starting despite initialization errors. Some features may be unavailable.");
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

static void EnsureJoinRequestsSchema(AppDbContext dbContext)
{
    dbContext.Database.ExecuteSqlRaw(@"
IF OBJECT_ID('dbo.TeamJoinRequests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TeamJoinRequests (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_TeamJoinRequests PRIMARY KEY,
        FullName nvarchar(150) NOT NULL,
        Email nvarchar(250) NOT NULL,
        PhoneNumber nvarchar(30) NOT NULL,
        NationalId nvarchar(14) NULL,
        BirthDate date NULL,
        GovernorateId uniqueidentifier NOT NULL,
        CommitteeId uniqueidentifier NULL,
        Motivation nvarchar(3000) NOT NULL,
        Experience nvarchar(3000) NULL,
        Status nvarchar(30) NOT NULL CONSTRAINT DF_TeamJoinRequests_Status DEFAULT 'Pending',
        AdminNotes nvarchar(2000) NULL,
        AssignedToMemberId uniqueidentifier NULL,
        ReviewedByMemberId uniqueidentifier NULL,
        CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_TeamJoinRequests_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        ReviewedAtUtc datetime2 NULL,
        CONSTRAINT FK_TeamJoinRequests_Governorates_GovernorateId FOREIGN KEY (GovernorateId) REFERENCES dbo.Governorates (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_TeamJoinRequests_Committees_CommitteeId FOREIGN KEY (CommitteeId) REFERENCES dbo.Committees (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_TeamJoinRequests_Members_AssignedToMemberId FOREIGN KEY (AssignedToMemberId) REFERENCES dbo.Members (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_TeamJoinRequests_Members_ReviewedByMemberId FOREIGN KEY (ReviewedByMemberId) REFERENCES dbo.Members (Id) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TeamJoinRequests_Status' AND object_id = OBJECT_ID('dbo.TeamJoinRequests'))
BEGIN
    CREATE INDEX IX_TeamJoinRequests_Status ON dbo.TeamJoinRequests (Status);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TeamJoinRequests_CreatedAtUtc' AND object_id = OBJECT_ID('dbo.TeamJoinRequests'))
BEGIN
    CREATE INDEX IX_TeamJoinRequests_CreatedAtUtc ON dbo.TeamJoinRequests (CreatedAtUtc DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TeamJoinRequests_GovernorateId' AND object_id = OBJECT_ID('dbo.TeamJoinRequests'))
BEGIN
    CREATE INDEX IX_TeamJoinRequests_GovernorateId ON dbo.TeamJoinRequests (GovernorateId);
END;
");
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
        logger.LogError(
            exception,
            "Unhandled exception occurred. Method={Method} Path={Path} TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path.Value,
            context.TraceIdentifier);
        
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

app.UseAuthentication();

app.Use(async (context, next) =>
{
    var requestLogger = context.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("HttpRequest");
    var stopwatch = Stopwatch.StartNew();

    try
    {
        await next();
    }
    finally
    {
        stopwatch.Stop();

        var statusCode = context.Response.StatusCode;
        var memberId = context.User.GetMemberId()?.ToString() ?? "anonymous";
        var method = context.Request.Method;
        var path = context.Request.Path.Value;
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        var traceId = context.TraceIdentifier;

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            requestLogger.LogError(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.000} ms. TraceId={TraceId} MemberId={MemberId}",
                method,
                path,
                statusCode,
                elapsedMs,
                traceId,
                memberId);
        }
        else if (statusCode >= StatusCodes.Status400BadRequest)
        {
            requestLogger.LogWarning(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.000} ms. TraceId={TraceId} MemberId={MemberId}",
                method,
                path,
                statusCode,
                elapsedMs,
                traceId,
                memberId);
        }
        else
        {
            requestLogger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.000} ms. TraceId={TraceId} MemberId={MemberId}",
                method,
                path,
                statusCode,
                elapsedMs,
                traceId,
                memberId);
        }
    }
});

app.UseMiddleware<PasswordChangeRequiredMiddleware>();
app.UseMiddleware<AuditRequestContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
