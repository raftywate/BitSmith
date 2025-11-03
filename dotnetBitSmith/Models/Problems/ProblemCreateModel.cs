using System;
using System.Collections.Generic;
using dotnetBitSmith.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Problems {
    public class ProblemCreateModel {
        [Required]
        [StringLength(100, MinimumLength = 5)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(5000, MinimumLength = 10)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public ProblemDifficulty Difficulty { get; set; }

        public string? StarterCode { get; set; }

        [Required]
        public List<Guid> CategoryIDs { get; set; }
    }
}