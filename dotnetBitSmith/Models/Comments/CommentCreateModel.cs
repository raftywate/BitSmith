using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Comments {
    public class CommentCreateModel {
        [Required]
        public Guid SolutionId { get; set; }
        public Guid? ParentCommentId { get; set; }

        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Content { get; set; }
    }
}