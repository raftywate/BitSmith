using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnetBitSmith.Entities {
    public class TestCase {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Input { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string ExpectedOutput { get; set; }

        [Required]
        public bool IsHidden { get; set; }

        //---Foreign Keys---
        [Required]
        public Guid ProblemId { get; set; }
        
        //---Navigation Property---
        //This is the Navigation Property that lets EF Core
        //link this TestCase back to its parent Problem.

        [ForeignKey(nameof(ProblemId))]
        public virtual Problem Problem { get; set; }
    }
}