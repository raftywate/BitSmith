using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Solutions {
    public class SolutionUpdateModel {
        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }
    }
}
