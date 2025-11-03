using System;
using dotnetBitSmith.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnetBitSmith.Entities {
    public class ProblemCategory {
        //---Foreign Keys---
        public Guid ProblemId { get; set; }

        public Guid CategoryId { get; set; }

        //---Navigation properties---
        [ForeignKey(nameof(ProblemId))]
        public virtual Problem Problem { get; set; }
        [ForeignKey(nameof(CategoryId))]
        public virtual Category Category { get; set; }

    }
}