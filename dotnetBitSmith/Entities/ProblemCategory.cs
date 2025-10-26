using System;
using dotnetBitSmith.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnetBitSmith.Entities {
    public class ProblemCategory {
        [ForeignKey(nameof(Problem))]
        public Guid ProblemId { get; set; }
        
        [ForeignKey(nameof(Category))]
        public Guid CategoryId { get; set; }

        //--Navigation properties--
        public virtual Problem Problems { get; set; }
        public virtual Category Categories { get; set; }
        
    }
}