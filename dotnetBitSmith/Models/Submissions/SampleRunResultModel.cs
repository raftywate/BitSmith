namespace dotnetBitSmith.Models.Submissions {
    public class SampleRunResultModel {
        public Guid TestCaseId { get; set; }
        public string Input { get; set; } = string.Empty;
        public string ExpectedOutput { get; set; } = string.Empty;
        public string ActualOutput { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Error { get; set; }
        public int? ExecutionTimeMs { get; set; }
        public int? ExecutionMemoryKb { get; set; }
        public bool Passed { get; set; }
    }
}
