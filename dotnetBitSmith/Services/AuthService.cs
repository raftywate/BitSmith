using System;
using System.Text;
using dotnetBitSmith.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Exceptions;
using dotnetBitSmith.Interfaces;
using System.Collections.Generic;
using dotnetBitSmith.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;

namespace dotnetBitSmith.Services {
    public class AuthService : IAuthService {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration) {
            _context = context;
            _configuration = configuration;
        }

        public async Task<AuthResponseModel> RegisterAsync(UserRegisterModel model)
        {
            var userExists = _context.Users.AnyAsync(u => u.Email == model.Email);
            if (userExists != null)
            {
                throw new DuplicateUserException("User with this email already exists.");
            }

            userExists = _context.Users.AnyAsync(u => u.Username == model.Username);
            if (userExists != null)
            {
                throw new DuplicateUserException("User with this username already exists.");
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = model.Username,
                Email = model.Email,
                PasswordHash = passwordHash,
                UserRole = "User" // Default role
                // Other properties (like CreatedAt) have default values
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            return GenerateJwtToken(user);
        }

        public async Task<AuthResponseModel> LoginAsync(UserLoginModel model) {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash)) {
                throw new InvalidLoginException("Invalid email or password.");
            }

            return GenerateJwtToken(user);
        }

        
        private AuthResponseModel GenerateJwtToken(User user) {
            // Get settings from appsettings.json
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["Key"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];

            if (string.IsNullOrEmpty(secretKey)) {
                throw new InvalidOperationException("JWT Secret Key is not configured in appsettings.json");
            }

            // 1. Create the "claims" for the token
            // These are the "facts" about the user that the token will contain.
            var claims = new List<Claim> {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), // "Subject" (the user's unique ID)
                new Claim(JwtRegisteredClaimNames.Name, user.Username),     // "Name"
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // "JWT ID" (a unique ID for this token)
                new Claim(ClaimTypes.Role, user.UserRole)                        // The user's role
            };

            // 2. Create the signing key
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 3. Define the token
            var tokenDescriptor = new SecurityTokenDescriptor {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1), // Token is valid for 1 hour
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = creds
            };

            // 4. Create and write the token
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // 5. Return the response model
            return new AuthResponseModel {
                Token = tokenString,
                UserId = user.Id,
                Username = user.Username,
                Role = user.UserRole
            };
        }
    }
}