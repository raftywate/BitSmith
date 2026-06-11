using System.Collections.Generic;

namespace dotnetBitSmith.Models.Problems {
    public class ImportResultModel {
        public int TotalFound { get; set; }
        public int SuccessfullyImported { get; set; }
        public int SkippedDuplicates { get; set; }
        public int Errors { get; set; }
        public List<string> ErrorMessages { get; set; } = new List<string>();
    }
}
