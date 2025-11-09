using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Users {
    public class UserProfileUpdateModel {
        [StringLength(100)]
        public string? DisplayName { get; set; }
        
        [StringLength(500)] 
        public string? Bio { get; set; }

        [StringLength(512)] 
        public string? ProfilePictureUrl { get; set; }
    }
}