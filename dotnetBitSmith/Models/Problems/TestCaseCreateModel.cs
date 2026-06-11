using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Problems {
    public class TestCaseCreateModel {
        [Required]
        public string Input { get; set; } = string.Empty;

        [Required]
        public string ExpectedOutput { get; set; } = string.Empty;

        public List<string> InputLabels { get; set; } = new();

        public bool IsHidden { get; set; } = true;
    }
}
