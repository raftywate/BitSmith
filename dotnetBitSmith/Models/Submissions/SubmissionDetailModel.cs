using dotnetBitSmith.Entities.Enums;

namespace dotnetBitSmith.Models.Submissions {
    public class SubmissionDetailModel {
        //Id, problemId,Status,Language,Code, executionTime, executionMemoryKb, CreatedAt
        public Guid Id { get; set; }
        public Guid ProblemId { get; set; }
        public string Code { get; set; }
        public string Language { get; set; }
        public SubmissionStatus Status { get; set; }
        public int? ExecutionTimeMs { get; set; }
        public int? ExecutionMemoryKb { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}