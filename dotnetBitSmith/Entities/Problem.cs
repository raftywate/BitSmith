using dotnetBitSmith.Entities.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnetBitSmith.Entities {
    public class Problem {
        // The [DatabaseGenerated] attribute tells SQL Server to handle the numbering (1, 2, 3...)
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProblemNumber { get; set; }
        
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Description { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? StarterCode { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(20)")]
        public ProblemDifficulty Difficulty { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        //---Foreign Keys---
        //The user(admin) who authored the problem
        public Guid? AuthorId { get; set; }

        //--Navigation Properties---

        [ForeignKey(nameof(AuthorId))]
        public virtual User Author { get; set; }
        public virtual ICollection<ProblemCategory> ProblemCategories { get; set; } = new List<ProblemCategory>();
        public virtual ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();
        public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
        public virtual ICollection<Solution> Solutions { get; set; } = new List<Solution>();

    }   
}