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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

AppContext.SetSwitch("System.Net.DisableIPv6", true);

// Prevent inotify limit issues on Docker/Render by disabling reloadOnChange for configuration
// and switching file monitoring to polling mode as a fallback.
Environment.SetEnvironmentVariable("hostBuilder__reloadConfigOnChange", "false");
Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");

var builder = WebApplication.CreateBuilder(args);
builder.Environment.WebRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
const string DEV_CORS_POLICY = "AllowDevOrigin";

builder.Services.AddCors(options => {
    options.AddPolicy(name: DEV_CORS_POLICY, policy => {
          policy.AllowAnyOrigin()
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

// Validate that the host resolves to at least one IPv4 address to prevent Npgsql DivideByZeroException when IPv6 is disabled
try {
    var cb = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
    var host = cb.Host;
    if (!string.IsNullOrEmpty(host) && !System.Net.IPAddress.TryParse(host, out _)) {
        var addresses = System.Net.Dns.GetHostAddresses(host);
        var ipv4Addresses = System.Linq.Enumerable.Where(addresses, ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        if (!System.Linq.Enumerable.Any(ipv4Addresses)) {
            throw new InvalidOperationException($"The database host '{host}' does not resolve to any IPv4 addresses. " +
                "If you are using Supabase, please ensure you are using the connection pooler hostname (which supports IPv4) " +
                "instead of the direct connection string, as Supabase direct hosts are IPv6-only. " +
                "For Supabase, the pooled hostname ends with '.pooler.supabase.com'.");
        }
    }
} catch (Exception ex) when (ex is not InvalidOperationException) {
    // Ignore other DNS or connection string format errors here and let Npgsql handle them
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
builder.Services.AddScoped<ICompilationService, Judge0CompilationService>();
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
    var logger = services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Ensuring database tables are initialized...");
    try {
        context.Database.EnsureCreated();
    } catch (Exception ex) {
        logger.LogWarning(ex, "EnsureCreated failed or did not create tables. Attempting direct table creation...");
    }

    var databaseCreator = context.Database.GetService<IDatabaseCreator>() as IRelationalDatabaseCreator;
    if (databaseCreator != null) {
        try {
            databaseCreator.CreateTables();
            logger.LogInformation("Database tables created successfully.");
        } catch (Exception ex) {
            // Ignore error if tables already exist
            logger.LogInformation("Database tables initialization check completed (tables may already exist): {Message}", ex.Message);
        }
    }

    // Automatic seeding if database has no problems
    try {
        if (!context.Problems.Any()) {
            logger.LogInformation("No problems found in database. Running automatic seeding of LeetCode problems...");
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "problems.json");
            if (System.IO.File.Exists(filePath)) {
                try {
                    context.Database.ExecuteSqlRaw(@"
                        ALTER TABLE ""Problems"" ADD COLUMN IF NOT EXISTS ""HintsJson"" TEXT NULL;
                    ");
                    context.Database.ExecuteSqlRaw(@"
                        ALTER TABLE ""TestCases"" ADD COLUMN IF NOT EXISTS ""InputLabelsJson"" TEXT NULL;
                    ");
                } catch { }

                var jsonContent = System.IO.File.ReadAllText(filePath);
                var result = dotnetBitSmith.Helpers.ProblemSeeder.SeedProblemsFromJsonAsync(
                    jsonContent, context, logger, null, false, 75, 100, 50).GetAwaiter().GetResult();
                logger.LogInformation("Automatic seeding completed! Successfully imported: {Imported}, Errors: {Errors}", result.SuccessfullyImported, result.Errors);
            } else {
                logger.LogWarning("problems.json file not found at {Path}. Skipping automatic seeding.", filePath);
            }
        }
    } catch (Exception ex) {
        logger.LogError(ex, "Failed to run automatic database seeding on startup.");
    }

    // Recommendation C: Ensure index existence on local database
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
            var sandboxContainer = app.Configuration["SandboxSettings:CppContainerName"] ?? "bitsmith-sandbox-gcc";
            logger.LogInformation("Checking and precompiling stdc++.h in {ContainerName} container...", sandboxContainer);
            var startInfo = new System.Diagnostics.ProcessStartInfo {
                FileName = "docker",
                Arguments = $"exec {sandboxContainer} sh -lc \"header=$(find /usr/local/include/c++ /usr/include/c++ -path '*/bits/stdc++.h' -print -quit 2>/dev/null); if [ -f \\\"$header\\\" ] && [ ! -f \\\"$header.gch\\\" ]; then cd $(dirname \\\"$header\\\") && timeout 25s g++ -w -std=c++23 stdc++.h; fi\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(startInfo);
            if (proc != null) {
                await proc.WaitForExitAsync();
                logger.LogInformation("{ContainerName} stdc++.h precompilation check/run completed.", sandboxContainer);
            }
        } catch (Exception ex) {
            logger.LogWarning(ex, "Failed to precompile stdc++.h in bitsmith-sandbox-gcc container.");
        }
    });

    // Warm up the Judge0 VM by sending a dummy C++ submission at startup.
    // This primes the OS page cache on the VM so the first real user submission
    // compiles in ~9s instead of ~30s (cold start penalty).
    _ = Task.Run(async () => {
        try {
            var useDockerForCpp = app.Configuration.GetValue("SandboxSettings:UseDockerForCpp", true);
            if (useDockerForCpp) return;

            var judge0Url = app.Configuration["Judge0Settings:ApiUrl"] ?? "http://localhost:2358";
            var judge0Key = app.Configuration["Judge0Settings:ApiKey"] ?? "";

            // Only warm up if pointing at a self-hosted VM (not RapidAPI)
            if (judge0Url.Contains("rapidapi", StringComparison.OrdinalIgnoreCase)) return;

            await Task.Delay(5000); // Give containers time to be fully ready

            logger.LogInformation("Warming up Judge0 VM with a dummy C++ submission...");
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(90);
            if (!string.IsNullOrEmpty(judge0Key)) {
                httpClient.DefaultRequestHeaders.Add("X-RapidAPI-Key", judge0Key);
            }

            var warmupPayload = new {
                source_code = "#include<bits/stdc++.h>\nusing namespace std;\nint main(){cout<<1;return 0;}",
                language_id = 54,
                stdin = ""
            };
            var json = System.Text.Json.JsonSerializer.Serialize(warmupPayload);
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"{judge0Url}/submissions?base64_encoded=false&wait=true", content);
            logger.LogInformation("Judge0 VM warm-up completed. Status: {Status}", response.StatusCode);
        } catch (Exception ex) {
            logger.LogWarning(ex, "Judge0 VM warm-up failed (non-critical).");
        }
    });
}

app.Run();
