using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnetBitSmith.Entities {
    public class ProblemOfTheDay {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public DateOnly Date { get; set; }

        [Required]
        public Guid ProblemId { get; set; }

        [ForeignKey(nameof(ProblemId))]
        public virtual Problem Problem { get; set; }
    }
}
