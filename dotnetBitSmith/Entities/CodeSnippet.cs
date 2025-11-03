using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnetBitSmith.Entities {
    public class CodeSnippet {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        [StringLength(50)]
        public string Language { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Code { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- Foreign Key ---
        [Required]
        public Guid UserId { get; set; }

        // --- Navigation Property ---
        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; }
    }
}