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
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using dotnetBitSmith.Entities;

//When in the "Development" environment, this line automatically does two things:
//It loads appsettings.json.
//It then automatically loads User Secrets file, overriding any settings from the first file.
//So, when AuthService asks for JwtSettings:Key, the configuration manager will find it in the
//User Secrets and provide it.
var builder = WebApplication.CreateBuilder(args);
const string DEV_CORS_POLICY = "AllowDevOrigin";

// --- Add services to the container ---

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: DEV_CORS_POLICY,
      policy =>
      {
          // This policy allows your Angular app (at http://localhost:4200)
          // to talk to your .NET API (at http://localhost:5078)
          policy.WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod();
      });
});

// 1. Add Controller support (this is the key line)
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        // This converter allows the API to accept "Easy" as a string
        // and correctly map it to the ProblemDifficulty.Easy enum (value 0).
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });


// 2. Configure Swagger/OpenAPI
builder.Services.AddSwaggerGen(options => {
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "BitSmith API", Version = "v1" });
    // Add JWT "Authorize" button to Swagger UI
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
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[]{ }
        }
    });
});

// 3. Get the connection string from appsettings.json
var connectionString = builder.Configuration.GetConnectionString("connectionString")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// 4. Register the ApplicationDbContext with the service container
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

//5. Configuring Rate Limiting
builder.Services.AddRateLimiter(options => {
    //Defining a "fixed window" policy named "auth-policy"
    options.AddFixedWindowLimiter(policyName: "auth-policy", opt => {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 5;
    });

    options.AddFixedWindowLimiter(policyName: "submit-policy", opt => {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
    
    options.AddFixedWindowLimiter(policyName: "post-content-policy", opt => {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromHours(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
});

//6. Create JWT Authentication
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

//Configure Authorization
builder.Services.AddAuthorization();

builder.Services.AddHttpClient();

//Configure Custom services(Dependency Injection)
builder.Services.AddScoped<IVoteService, VoteService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IProblemService, ProblemService>();
builder.Services.AddScoped<ISolutionService, SolutionService>();
builder.Services.AddScoped<ISubmissionService, SubmissionService>();
builder.Services.AddScoped<ICompilationService, Judge0CompilationService>(); // <-- THIS IS NEW

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Configure the HTTP request pipeline

var app = builder.Build();

// --- Configure the HTTP request pipeline ---
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Adding the custom "safety net" middleware FIRST
app.UseExceptionHandlingMiddleware();

app.UseHttpsRedirection();

app.UseCors(DEV_CORS_POLICY);

// Add the Rate Limiter to the pipeline
app.UseRateLimiter();
// Add authentication (who are you?)
app.UseAuthentication();
// Add authorization (are you allowed?)
app.UseAuthorization();

// This line tells the app to find and use Controller files
app.MapControllers();

app.Run();