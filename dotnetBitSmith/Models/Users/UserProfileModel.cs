namespace dotnetBitSmith.Models.Users {
    public class UserProfileModel {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string? DisplayName { get; set; }
        public string? Bio { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}