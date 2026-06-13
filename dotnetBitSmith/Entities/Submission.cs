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
        public string Code { get; set; }

        [Required]
        public SubmissionStatus Status { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? ExecutionTimeMs { get; set; }
        public int? ExecutionMemoryKb { get; set; }
        public string? ErrorMessage { get; set; }
        public int PassedCount { get; set; }
        public int TotalCount { get; set; }
        public string? FailedTestCaseInput { get; set; }
        public string? FailedTestCaseExpected { get; set; }
        public string? FailedTestCaseActual { get; set; }

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