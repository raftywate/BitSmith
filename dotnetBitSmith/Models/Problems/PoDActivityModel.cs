using System;
using System.Collections.Generic;

namespace dotnetBitSmith.Models.Problems {
    public class PoDActivityModel {
        public List<string> SolvedDates { get; set; } = new List<string>();
        public int CurrentStreak { get; set; }
    }
}
