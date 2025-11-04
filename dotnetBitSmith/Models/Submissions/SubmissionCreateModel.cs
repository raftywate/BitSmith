using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Submissions {
    public class SubmissionCreateModel {
        [Required]
        public Guid ProblemId { get; set; }
        
        [Required]
        [StringLength(20, ErrorMessage = "Language cannot exceed 20 characters.")]
        public string Language { get; set; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(10000, ErrorMessage = "Code submission cannot exceed 10,000 characters.")]
        public string Code { get; set; }

    }
}