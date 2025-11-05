using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Solutions {
    public class SolutionCreateModel {
        [Required]
        public Guid ProblemId { get; set; }
        
        [Required]
        [StringLength(100, MinimumLength = 10)]
        public string Title { get; set; }

        [Required]
        [StringLength(10000, MinimumLength = 50)]
        public string Content { get; set; }
    }
}