using dotnetBitSmith.Entities.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnetBitSmith.Entities {
    public class Vote {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid EntityId { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(20)")]
        public VotableEntityType EntityType { get; set; }

        [Required]
        public bool IsUpvote { get; set; }
        
        //---Foreign Keys---
        [Required]
        public Guid UserId { get; set; }

        //---Navigation Property---
        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; }
    }
}