using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Auth {
    public class UserLoginModel {
        [Required]
        public string EmailOrUsername { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
