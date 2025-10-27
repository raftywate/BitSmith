using System;
using dotnetBitSmith.Entities.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnetBitSmith.Entities {
    public class Submission {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(50)]
        public string Language { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Code { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(20)")]
        public SubmissionStatus Status { get; set; }

        public int? ExecutionTimeMs { get; set; }
        public int? ExecutionMemoryKb { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        //---Foreign Keys---
        [Required]
        public Guid UserId { get; set; }
        
        [Required]
        public Guid ProblemId { get; set; }

        //---Navigation Property---

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; }

        [ForeignKey(nameof(ProblemId))]
        public virtual Problem Problem { get; set; }
    }
}