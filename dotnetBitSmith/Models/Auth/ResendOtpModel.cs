using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Auth {
    public class ResendOtpModel {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
