using dotnetBitSmith.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Submissions {
    public class SubmissionResultModel {
        public Guid Id { get; set; }
        public Guid ProblemId { get; set; }
        public SubmissionStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public int PassedCount { get; set; }
        public int TotalCount { get; set; }
        public string? FailedTestCaseInput { get; set; }
        public string? FailedTestCaseExpected { get; set; }
        public string? FailedTestCaseActual { get; set; }

    }
}