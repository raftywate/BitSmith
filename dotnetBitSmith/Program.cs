using System.Text;
using dotnetBitSmith.Data;
using dotnetBitSmith.Services;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;

//When in the "Development" environment, this line automatically does two things:
//It loads appsettings.json.
//It then automatically loads User Secrets file, overriding any settings from the first file.
//So, when AuthService asks for JwtSettings:Key, the configuration manager will find it in the
//User Secrets and provide it.
var builder = WebApplication.CreateBuilder(args);

// --- Add services to the container ---

// 1. Get the connection string from appsettings.json
var connectionString = builder.Configuration.GetConnectionString("connectionString")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// 2. Register the ApplicationDbContext with the service container
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 3. Add Controller support (this is the key line)
builder.Services.AddControllers();

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Key"]))
    };
});
// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- Configure the HTTP request pipeline ---
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseExceptionHandlingMiddleware();

app.UseAuthentication();

app.UseAuthorization();

// This line tells the app to find and use Controller files
app.MapControllers();

app.Run();