namespace dotnetBitSmith.Models.Auth {
    public class AuthResponseModel {
        public string Token { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
    }
}