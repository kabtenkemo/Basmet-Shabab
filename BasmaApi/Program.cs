using System.Text;
using BasmaApi.Data;
using BasmaApi.Middleware;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.Equals(origin, "http://localhost:5173", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (origin.EndsWith(".netlify.app", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddScoped<IPasswordService, BcryptPasswordService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is missing.");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is missing.");

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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StartupInitialization");

    try
    {
        if (app.Environment.IsProduction())
        {
            var currentConnection = dbContext.Database.GetConnectionString() ?? string.Empty;
            if (currentConnection.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is still pointing to LocalDB in Production.");
            }
        }

        var applyEfMigrations = app.Configuration.GetValue<bool>("Startup:ApplyEfMigrations");
        if (app.Environment.IsDevelopment() || applyEfMigrations)
        {
            dbContext.Database.Migrate();
        }
        else
        {
            startupLogger.LogWarning("Skipping EF Core migrations on startup in Production. Set Startup:ApplyEfMigrations=true to force migration execution.");
        }

        EnsureMemberIdentityColumns(dbContext);
        EnsureComplaintAuditSchema(dbContext);
        EnsureReferenceDataSchema(dbContext);
        EnsureTaskSchema(dbContext);
        EnsureNewsSchema(dbContext);
        SeedReferenceData(dbContext);

        var president = dbContext.Members.FirstOrDefault(member => member.Role == MemberRole.President && member.Email == "president@basmet.local")
            ?? dbContext.Members.FirstOrDefault(member => member.Role == MemberRole.President);

        if (president is null)
        {
            var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            president = new Member
            {
                FullName = "رئيس الكيان",
                Email = "president@basmet.local",
                NationalId = "00000000000001",
                BirthDate = new DateOnly(1980, 1, 1),
                Role = MemberRole.President,
                Points = 0
            };

            president.PasswordHash = passwordService.HashPassword("123");
            dbContext.Members.Add(president);
            dbContext.SaveChanges();
        }
        else
        {
            var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
            president.FullName = string.IsNullOrWhiteSpace(president.FullName) ? "رئيس الكيان" : president.FullName;
            president.Email = president.Email == string.Empty ? "president@basmet.local" : president.Email;
            president.NationalId = string.IsNullOrWhiteSpace(president.NationalId) ? "00000000000001" : president.NationalId;
            president.BirthDate ??= new DateOnly(1980, 1, 1);
            president.PasswordHash = passwordService.HashPassword("123");
            dbContext.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Application failed during startup initialization.");
        throw;
    }
}

static void EnsureMemberIdentityColumns(AppDbContext dbContext)
{
    dbContext.Database.ExecuteSqlRaw("IF COL_LENGTH('dbo.Members', 'NationalId') IS NULL ALTER TABLE dbo.Members ADD NationalId nvarchar(14) NULL;");
    dbContext.Database.ExecuteSqlRaw("IF COL_LENGTH('dbo.Members', 'BirthDate') IS NULL ALTER TABLE dbo.Members ADD BirthDate date NULL;");
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

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors("ClientApp");

app.UseAuthentication();
app.UseMiddleware<AuditRequestContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
