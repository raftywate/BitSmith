using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Users {
    public class UserPreferencesUpdateModel {
        [StringLength(50)]
        public string? PreferredLanguage { get; set; }

        public string? LayoutState { get; set; }
    }
}
