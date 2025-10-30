using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnetBitSmith.Entities {
    public class Comment {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Content { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        //---Foreign Keys---

        public Guid? UserId { get; set; }

        [Required]
        public Guid SolutionId { get; set; }

        public Guid? ParentCommentId { get; set; }

        //---Navigation Properties---

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; }

        [ForeignKey(nameof(SolutionId))]
        public virtual Solution Solution { get; set; }

        [ForeignKey(nameof(ParentCommentId))]
        public virtual Comment ParentComment { get; set; }

        public ICollection<Comment> Replies { get; set; } = new List<Comment>();
        public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    }
}