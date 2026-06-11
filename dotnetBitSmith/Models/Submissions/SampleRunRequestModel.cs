namespace dotnetBitSmith.Models.Submissions {
    public class SampleRunRequestModel {
        public Guid ProblemId { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
