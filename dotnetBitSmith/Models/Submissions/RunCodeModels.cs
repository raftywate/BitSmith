namespace dotnetBitSmith.Models.Submissions {
    public class RunCodeRequestModel {
        public string Language { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string CustomStdin { get; set; } = string.Empty;
    }

    public class RunCodeResultModel {
        public string Stdout { get; set; } = string.Empty;
        public string Stderr { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? ExecutionTimeMs { get; set; }
    }
}
