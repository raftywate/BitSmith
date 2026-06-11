namespace dotnetBitSmith.Models.Problems {
    public class TestCaseModel {
        public Guid Id { get; set; }
        public string Input { get; set; } = string.Empty;
        public string ExpectedOutput { get; set; } = string.Empty;
        public List<string> InputLabels { get; set; } = new();
        public bool IsHidden { get; set; }
    }
}
