using dotnetBitSmith.Data;
using dotnetBitSmith.Exceptions;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Users;
using Microsoft.EntityFrameworkCore;

namespace dotnetBitSmith.Services {
    public class UserService : IUserService {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(ApplicationDbContext context, ILogger<UserService> logger) {
            _context = context;
            _logger = logger;
        }

        public async Task<UserProfileModel> GetMyProfileAsync(Guid userId) {
            _logger.LogInformation("Fetching profile for User {UserId}", userId);

            var user = await _context.Users.
                AsNoTracking().
                FirstOrDefaultAsync(u => u.Id == userId);

            if(user == null) {
                // This should be impossible if [Authorize] is working, but it's a critical safety check
                _logger.LogWarning("GetMyProfileAsync failed: User {UserId} not found.", userId);
                throw new NotFoundException($"User with ID {userId} not found.");
            }

            return new UserProfileModel {
                Id = user.Id,
                Bio = user.Bio,
                Username = user.Username,
                CreatedAt = user.CreatedAt,
                DisplayName = user.DisplayName,
                ProfilePictureUrl = user.ProfilePictureUrl
            };
        }

        public async Task<UserProfileModel> GetProfileByIdAsync(Guid userId) {
             return await GetMyProfileAsync(userId);
        }

        public async Task<UserProfileModel> UpdateMyProfileAsync(Guid userId, UserProfileUpdateModel model) {
            _logger.LogInformation("Updating profile for User {UserId}", userId);

            var user = await _context.Users.
                FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) {
                _logger.LogWarning("UpdateMyProfileAsync failed: User {UserId} not found.", userId);
                throw new NotFoundException($"User with ID {userId} not found.");
            }

            user.Bio = model.Bio;
            user.DisplayName = model.DisplayName;
            user.ProfilePictureUrl = model.ProfilePictureUrl;

            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Profile for User {UserId} updated successfully.", userId);

            return await GetMyProfileAsync(userId);
        }
    }
}