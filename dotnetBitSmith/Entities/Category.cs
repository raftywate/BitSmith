using System;
using dotnetBitSmith.Entities;
using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Entities {
    public class Category {
        [Key]
        public Guid ProblemId { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(50)]
        public string Name { get; set; }

        [Required]
        [StringLength(50)]

        //slug is a url format version of the problem name 
        // i.e. "dynamic-programming" is the slug version of "dynamic programming"
        //"binary-search" is for "binary search"
        public string Slug { get; set; }

        //--Navigation properties--

        public virtual ICollection<ProblemCategory> ProblemCategories { get; set; } = new List<ProblemCategory>();
        
    }
}