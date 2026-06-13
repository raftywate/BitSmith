using System.Text;
using dotnetBitSmith.Data;
using System.Security.Claims;
using dotnetBitSmith.Services;
using Microsoft.OpenApi.Models;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Middleware;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using dotnetBitSmith.Entities;
using System.Net.Security;

AppContext.SetSwitch("System.Net.DisableIPv6", true);

var builder = WebApplication.CreateBuilder(args);
builder.Environment.WebRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
const string DEV_CORS_POLICY = "AllowDevOrigin";

builder.Services.AddCors(options => {
    options.AddPolicy(name: DEV_CORS_POLICY, policy => {
          policy.WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod();
      });
});

builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddSwaggerGen(options => {
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Compylr API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
        In = ParameterLocation.Header,
        Description = "Please enter token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[]{ }
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("connectionString");
if (string.IsNullOrEmpty(connectionString)) {
    connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
}

if (string.IsNullOrEmpty(connectionString)) {
    throw new InvalidOperationException("Connection string 'connectionString' or 'DATABASE_URL' not found.");
}

if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || 
    connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)) {
    try {
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo[0];
        var password = userInfo.Length > 1 ? userInfo[1] : string.Empty;
        var host = uri.Host;
        var port = uri.Port == -1 ? 5432 : uri.Port;
        var database = uri.LocalPath.TrimStart('/');
        
        connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
    } catch (Exception ex) {
        throw new FormatException("Failed to parse PostgreSQL URL connection string.", ex);
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));


builder.Services.AddRateLimiter(options => {
    options.AddPolicy("auth-policy", context => 
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions {
                AutoReplenishment = true,
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }
        )
    );

    options.AddPolicy("submit-policy", context => 
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions {
                AutoReplenishment = true,
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }
        )
    );
    
    options.AddFixedWindowLimiter(policyName: "post-content-policy", opt => {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromHours(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        var key = jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key is not configured.");

        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpClient();

builder.Services.AddScoped<IVoteService, VoteService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IProblemService, ProblemService>();
builder.Services.AddScoped<ISolutionService, SolutionService>();
builder.Services.AddScoped<ISubmissionService, SubmissionService>();
builder.Services.AddScoped<ICompilationService, DockerCompilationService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// Register memory cache & background submission worker dependencies
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISubmissionQueue, SubmissionQueue>();
builder.Services.AddHostedService<SubmissionProcessingWorker>();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandlingMiddleware();
app.UseHttpsRedirection();
app.UseCors(DEV_CORS_POLICY);
app.UseStaticFiles();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (args.Contains("--import-leetcode")) {
    using (var scope = app.Services.CreateScope()) {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Startup seeder: Starting LeetCode problems import...");
        try {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "problems.json");
            if (System.IO.File.Exists(filePath)) {
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE ""Problems"" ADD COLUMN IF NOT EXISTS ""HintsJson"" TEXT NULL;
                ");
                await context.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE ""TestCases"" ADD COLUMN IF NOT EXISTS ""InputLabelsJson"" TEXT NULL;
                ");
                var jsonContent = System.IO.File.ReadAllText(filePath);
                var result = dotnetBitSmith.Helpers.ProblemSeeder.SeedProblemsFromJsonAsync(
                    jsonContent, context, logger, null, true, 75, 100, 50).GetAwaiter().GetResult();
                logger.LogInformation("Startup seeder: Import completed! Imported: {Imported}, Errors: {Errors}", result.SuccessfullyImported, result.Errors);
                if (result.ErrorMessages.Any()) {
                    logger.LogWarning("Errors encountered during seeding:\n{Errors}", string.Join("\n", result.ErrorMessages));
                }
            }
        } catch (Exception ex) {
            logger.LogError(ex, "Failed to run startup seeder");
        }
    }
    return;
}

using (var scope = app.Services.CreateScope()) {
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();

    // Recommendation C: Ensure index existence on local database
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Verifying and creating composite database indexes...");
    try {
        context.Database.ExecuteSqlRaw(@"
            CREATE INDEX IF NOT EXISTS ""IX_Submissions_UserId_ProblemId_Status_CreatedAt"" 
            ON ""Submissions"" (""UserId"", ""ProblemId"", ""Status"", ""CreatedAt"");
        ");

        context.Database.ExecuteSqlRaw(@"
            CREATE INDEX IF NOT EXISTS ""IX_ProblemOfTheDays_Date"" 
            ON ""ProblemOfTheDays"" (""Date"");
        ");
        logger.LogInformation("Database indexes verified successfully.");
    } catch (Exception ex) {
        logger.LogError(ex, "Failed to create composite database indexes on startup.");
    }

    logger.LogInformation("Verifying and adding User verification columns...");
    try {
        context.Database.ExecuteSqlRaw(@"
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""EmailVerificationOtp"" VARCHAR(10) NULL;
        ");

        context.Database.ExecuteSqlRaw(@"
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""EmailVerificationOtpExpiry"" TIMESTAMP NULL;
        ");
        logger.LogInformation("User verification columns verified successfully.");
    } catch (Exception ex) {
        logger.LogError(ex, "Failed to create User verification database columns on startup.");
    }

    // Precompile stdc++.h in bitsmith-sandbox-gcc container in background to speed up C++ judge times
    _ = Task.Run(async () => {
        try {
            logger.LogInformation("Checking and precompiling stdc++.h in bitsmith-sandbox-gcc container...");
            var startInfo = new System.Diagnostics.ProcessStartInfo {
                FileName = "docker",
                Arguments = "exec bitsmith-sandbox-gcc sh -c \"[ ! -f /usr/local/include/c++/13.4.0/x86_64-linux-gnu/bits/stdc++.h.gch ] && cd /usr/local/include/c++/13.4.0/x86_64-linux-gnu/bits/ && g++ -w -std=c++23 stdc++.h\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(startInfo);
            if (proc != null) {
                await proc.WaitForExitAsync();
                logger.LogInformation("bitsmith-sandbox-gcc stdc++.h precompilation check/run completed.");
            }
        } catch (Exception ex) {
            logger.LogWarning(ex, "Failed to precompile stdc++.h in bitsmith-sandbox-gcc container.");
        }
    });
}

app.Run();
