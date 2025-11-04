using dotnetBitSmith.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Submissions {
    public class SubmissionResultModel {
        public Guid Id { get; set; }
        public Guid ProblemId { get; set; }
        public SubmissionStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

    }
}