using dotnetBitSmith.Models.Users;

namespace dotnetBitSmith.Interfaces {
    public interface IUserService {
        Task<UserProfileModel> GetMyProfileAsync(Guid userId);
        Task<UserProfileModel> UpdateMyProfileAsync(Guid userId, UserProfileUpdateModel model);
    }
}