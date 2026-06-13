using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Auth {
    public class VerifyOtpModel {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(10)]
        public string Otp { get; set; }
    }
}
