using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Comments {
    public class CommentUpdateModel {
        [Required]
        public string Content { get; set; }
    }
}
