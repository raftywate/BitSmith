using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnetBitSmith.Entities {
    public class Solution {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]

        [Column(TypeName = "nvarchar(max)")]
        public string Content { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        //---Foreign Keys---

        [Required]
        public Guid ProblemId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        //---Navigation Property---

        [ForeignKey(nameof(ProblemId))]
        public virtual Problem Problem { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; }
        public virtual ICollection<Vote> Votes { get; set; }
        public virtual ICollection<Comment> Comments { get; set; }
    }
}