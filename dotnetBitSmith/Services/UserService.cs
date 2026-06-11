using dotnetBitSmith.Data;
using dotnetBitSmith.Entities.Enums;
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
                // Impossible if [Authorize] is working
                _logger.LogWarning("GetMyProfileAsync failed: User {UserId} not found.", userId);
                throw new NotFoundException($"User with ID {userId} not found.");
            }

            return new UserProfileModel {
                Id = user.Id,
                Bio = user.Bio,
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                DisplayName = user.DisplayName,
                ProfilePictureUrl = user.ProfilePictureUrl,
                Stats = await BuildStatsAsync(userId),
                PreferredLanguage = user.PreferredLanguage,
                LayoutState = user.LayoutState
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

            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Profile for User {UserId} updated successfully.", userId);

            return await GetMyProfileAsync(userId);
        }

        public async Task<UserProfileModel> UpdateMyAvatarAsync(Guid userId, string? profilePictureUrl) {
            _logger.LogInformation("Updating avatar for User {UserId}", userId);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) {
                _logger.LogWarning("UpdateMyAvatarAsync failed: User {UserId} not found.", userId);
                throw new NotFoundException($"User with ID {userId} not found.");
            }

            user.ProfilePictureUrl = profilePictureUrl;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return await GetMyProfileAsync(userId);
        }

        public async Task UpdateMyPreferencesAsync(Guid userId, string? preferredLanguage, string? layoutState) {
            _logger.LogInformation("Updating preferences for User {UserId}", userId);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) {
                _logger.LogWarning("UpdateMyPreferencesAsync failed: User {UserId} not found.", userId);
                throw new NotFoundException($"User with ID {userId} not found.");
            }

            if (preferredLanguage != null) {
                user.PreferredLanguage = preferredLanguage;
            }
            if (layoutState != null) {
                user.LayoutState = layoutState;
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Preferences for User {UserId} updated successfully.", userId);
        }

        private async Task<UserStatsModel> BuildStatsAsync(Guid userId) {
            var acceptedSubmissions = await _context.Submissions
                .AsNoTracking()
                .Where(s => s.UserId == userId && s.Status == SubmissionStatus.Accepted)
                .Select(s => new {
                    s.ProblemId,
                    s.CreatedAt,
                    s.Problem.ProblemNumber,
                    s.Problem.Title,
                    s.Problem.Difficulty
                })
                .ToListAsync();

            var solvedProblems = acceptedSubmissions
                .GroupBy(s => s.ProblemId)
                .Select(group => group.OrderByDescending(s => s.CreatedAt).First())
                .OrderByDescending(s => s.CreatedAt)
                .ToList();

            var activity = acceptedSubmissions
                .GroupBy(s => DateOnly.FromDateTime(s.CreatedAt.ToLocalTime().Date))
                .Select(group => new UserActivityDayModel {
                    Date = group.Key,
                    Count = group.Count()
                })
                .OrderBy(day => day.Date)
                .ToList();

            return new UserStatsModel {
                TotalSolved = solvedProblems.Count,
                EasySolved = solvedProblems.Count(s => s.Difficulty == ProblemDifficulty.Easy),
                MediumSolved = solvedProblems.Count(s => s.Difficulty == ProblemDifficulty.Medium),
                HardSolved = solvedProblems.Count(s => s.Difficulty == ProblemDifficulty.Hard),
                CurrentStreak = CalculateCurrentStreak(activity),
                Activity = activity,
                AcceptedProblems = solvedProblems.Select(problem => new AcceptedProblemModel {
                    Id = problem.ProblemId,
                    ProblemNumber = problem.ProblemNumber,
                    Title = problem.Title,
                    Difficulty = problem.Difficulty,
                    AcceptedAt = problem.CreatedAt
                }).ToList()
            };
        }

        private static int CalculateCurrentStreak(List<UserActivityDayModel> activity) {
            var activeDates = activity.Select(day => day.Date).ToHashSet();
            if (!activeDates.Any()) {
                return 0;
            }

            var cursor = DateOnly.FromDateTime(DateTime.Now.Date);
            if (!activeDates.Contains(cursor)) {
                cursor = cursor.AddDays(-1);
            }

            var streak = 0;
            while (activeDates.Contains(cursor)) {
                streak++;
                cursor = cursor.AddDays(-1);
            }

            return streak;
        }

        public async Task<UserProfileModel> UpdateMySettingsAsync(Guid userId, UserSettingsUpdateModel model) {
            _logger.LogInformation("Updating settings for User {UserId}", userId);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) {
                _logger.LogWarning("UpdateMySettingsAsync failed: User {UserId} not found.", userId);
                throw new NotFoundException($"User with ID {userId} not found.");
            }

            // Update Username if provided and changed
            if (!string.IsNullOrWhiteSpace(model.Username) && !string.Equals(user.Username, model.Username, StringComparison.Ordinal)) {
                var exists = await _context.Users.AnyAsync(u => u.Username == model.Username && u.Id != userId);
                if (exists) {
                    throw new NotSupportedException("Username is already taken.");
                }
                user.Username = model.Username;
            }

            // Update Email if provided and changed
            if (!string.IsNullOrWhiteSpace(model.Email) && !string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase)) {
                var exists = await _context.Users.AnyAsync(u => u.Email == model.Email && u.Id != userId);
                if (exists) {
                    throw new NotSupportedException("Email is already registered.");
                }
                user.Email = model.Email;
            }

            // Update Password if new password is provided
            if (!string.IsNullOrWhiteSpace(model.NewPassword)) {
                if (string.IsNullOrWhiteSpace(model.CurrentPassword)) {
                    throw new NotSupportedException("Current password is required to set a new password.");
                }

                if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash)) {
                    throw new NotSupportedException("Incorrect current password.");
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Settings for User {UserId} updated successfully.", userId);
            return await GetMyProfileAsync(userId);
        }
    }
}
