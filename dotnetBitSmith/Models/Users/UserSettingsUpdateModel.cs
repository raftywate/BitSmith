using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Users {
    public class UserSettingsUpdateModel {
        [StringLength(50)]
        public string? Username { get; set; }

        [EmailAddress]
        [StringLength(256)]
        public string? Email { get; set; }

        public string? CurrentPassword { get; set; }

        public string? NewPassword { get; set; }
    }
}
