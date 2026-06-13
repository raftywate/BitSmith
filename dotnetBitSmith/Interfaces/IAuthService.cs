using dotnetBitSmith.Models.Auth;

namespace dotnetBitSmith.Interfaces {
    public interface IAuthService {
        Task<AuthResponseModel> RegisterAsync(UserRegisterModel model);
        Task<AuthResponseModel> LoginAsync(UserLoginModel model);
        Task<AuthResponseModel> VerifyOtpAsync(VerifyOtpModel model);
        Task ResendOtpAsync(ResendOtpModel model);
    }
}