using dotnetBitSmith.Models.Auth;

namespace dotnetBitSmith.Interfaces {
    public interface IAuthService {
        Task<AuthResponseModel> RegisterAsync(UserRegisterModel model);
        Task<AuthResponseModel> LoginAsync(UserLoginModel model);
    }
}