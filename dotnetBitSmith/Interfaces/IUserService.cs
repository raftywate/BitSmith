using dotnetBitSmith.Models.Users;

namespace dotnetBitSmith.Interfaces {
    public interface IUserService {
        Task<UserProfileModel> GetMyProfileAsync(Guid userId);
        Task<UserProfileModel> GetProfileByIdAsync(Guid userId);
        Task<UserProfileModel> UpdateMyProfileAsync(Guid userId, UserProfileUpdateModel model);
        Task<UserProfileModel> UpdateMyAvatarAsync(Guid userId, string? profilePictureUrl);
        Task UpdateMyPreferencesAsync(Guid userId, string? preferredLanguage, string? layoutState);
        Task<UserProfileModel> UpdateMySettingsAsync(Guid userId, UserSettingsUpdateModel model);
    }
}
