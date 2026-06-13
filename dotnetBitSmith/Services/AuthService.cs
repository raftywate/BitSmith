using System.Text;
using dotnetBitSmith.Data;
using System.Security.Claims;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Exceptions;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;

namespace dotnetBitSmith.Services {
    public class AuthService : IAuthService {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(ApplicationDbContext context, IConfiguration configuration, IEmailService emailService, ILogger<AuthService> logger) {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<AuthResponseModel> RegisterAsync(UserRegisterModel model) {
            // Delete existing unverified registration attempts with same email or username to avoid duplicate key conflicts
            var existingByEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (existingByEmail != null) {
                if (existingByEmail.IsEmailVerified) {
                    throw new DuplicateUserException("User with this email already exists.");
                }
                _context.Users.Remove(existingByEmail);
                await _context.SaveChangesAsync();
            }

            var existingByUsername = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username);
            if (existingByUsername != null) {
                if (existingByUsername.IsEmailVerified) {
                    throw new DuplicateUserException("User with this username already exists.");
                }
                _context.Users.Remove(existingByUsername);
                await _context.SaveChangesAsync();
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            // Check invite code — if it matches the configured secret, grant Admin role
            var configuredCode = _configuration["AdminSettings:InviteCode"];
            var role = (!string.IsNullOrEmpty(configuredCode) &&
                        !string.IsNullOrEmpty(model.InviteCode) &&
                        model.InviteCode.Trim() == configuredCode.Trim())
                       ? "Admin"
                       : "User";

            var random = new Random();
            var otp = random.Next(100000, 999999).ToString();

            var user = new User {
                Id = Guid.NewGuid(),
                Username = model.Username,
                Email = model.Email,
                PasswordHash = passwordHash,
                UserRole = role,
                IsEmailVerified = false,
                EmailVerificationOtp = otp,
                EmailVerificationOtpExpiry = DateTime.UtcNow.AddMinutes(15)
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            // Send verification email
            var subject = "Verify your Compylr Account";
            var body = $"<h3>Welcome to Compylr!</h3>" +
                       $"<p>Thank you for signing up. Please use the following 6-digit One-Time Password (OTP) to verify your email address:</p>" +
                       $"<h2 style='color: #00b8a3; letter-spacing: 2px;'>{otp}</h2>" +
                       $"<p>This code is valid for 15 minutes.</p>";

            try {
                await _emailService.SendEmailAsync(user.Email, subject, body);
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to send registration verification email. Auto-verifying user '{Username}' as fallback.", user.Username);
                user.IsEmailVerified = true;
                user.EmailVerificationOtp = null;
                user.EmailVerificationOtpExpiry = null;
                await _context.SaveChangesAsync();

                return GenerateJwtToken(user);
            }

            return new AuthResponseModel {
                RequiresVerification = true,
                UserId = user.Id,
                Username = user.Username
            };
        }

        public async Task<AuthResponseModel> LoginAsync(UserLoginModel model) {
            var login = model.EmailOrUsername.Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == login || u.Username == login);
            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash)) {
                throw new InvalidLoginException("Invalid email/username or password.");
            }

            if (!user.IsEmailVerified) {
                // Regenerate a fresh OTP for them
                var random = new Random();
                var otp = random.Next(100000, 999999).ToString();
                user.EmailVerificationOtp = otp;
                user.EmailVerificationOtpExpiry = DateTime.UtcNow.AddMinutes(15);
                await _context.SaveChangesAsync();

                var subject = "Verify your Compylr Account";
                var body = $"<h3>Please verify your email address</h3>" +
                           $"<p>You attempted to login, but your email is not verified yet. Use the following OTP to complete registration:</p>" +
                           $"<h2 style='color: #00b8a3; letter-spacing: 2px;'>{otp}</h2>" +
                           $"<p>This code is valid for 15 minutes.</p>";

                try {
                    await _emailService.SendEmailAsync(user.Email, subject, body);
                } catch (Exception ex) {
                    _logger.LogWarning(ex, "Failed to send login verification email. Auto-verifying user '{Username}' as fallback.", user.Username);
                    user.IsEmailVerified = true;
                    user.EmailVerificationOtp = null;
                    user.EmailVerificationOtpExpiry = null;
                    await _context.SaveChangesAsync();

                    user.LastLoginAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return GenerateJwtToken(user);
                }

                throw new UserVerificationRequiredException(user.Email, "Please verify your email address. A new OTP has been sent.");
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return GenerateJwtToken(user);
        }

        public async Task<AuthResponseModel> VerifyOtpAsync(VerifyOtpModel model) {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null) {
                throw new NotFoundException("User account not found.");
            }

            if (user.IsEmailVerified) {
                throw new DuplicateUserException("Email is already verified. Please log in.");
            }

            if (user.EmailVerificationOtp != model.Otp || 
                user.EmailVerificationOtpExpiry == null || 
                user.EmailVerificationOtpExpiry < DateTime.UtcNow) {
                throw new InvalidLoginException("Invalid or expired verification code.");
            }

            user.IsEmailVerified = true;
            user.EmailVerificationOtp = null;
            user.EmailVerificationOtpExpiry = null;
            await _context.SaveChangesAsync();

            return GenerateJwtToken(user);
        }

        public async Task ResendOtpAsync(ResendOtpModel model) {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null) {
                throw new NotFoundException("User account not found.");
            }

            if (user.IsEmailVerified) {
                throw new DuplicateUserException("Email is already verified. Please log in.");
            }

            var random = new Random();
            var otp = random.Next(100000, 999999).ToString();
            user.EmailVerificationOtp = otp;
            user.EmailVerificationOtpExpiry = DateTime.UtcNow.AddMinutes(15);
            await _context.SaveChangesAsync();

            var subject = "Verify your Compylr Account (New Code)";
            var body = $"<h3>New Verification Code</h3>" +
                       $"<p>Use the following 6-digit One-Time Password (OTP) to verify your email address:</p>" +
                       $"<h2 style='color: #00b8a3; letter-spacing: 2px;'>{otp}</h2>" +
                       $"<p>This code is valid for 15 minutes.</p>";

            await _emailService.SendEmailAsync(user.Email, subject, body);
        }

        
        private AuthResponseModel GenerateJwtToken(User user) {
            // Get settings from appsettings.json
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey   = jwtSettings["Key"];
            var issuer      = jwtSettings["Issuer"];
            var audience    = jwtSettings["Audience"];

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
                Expires = DateTime.UtcNow.AddDays(7), // Token is valid for 7 days
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
                Role = user.UserRole,
                ProfilePictureUrl = user.ProfilePictureUrl
            };
        }
    }
}
