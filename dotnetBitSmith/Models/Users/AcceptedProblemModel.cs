using dotnetBitSmith.Entities.Enums;

namespace dotnetBitSmith.Models.Users {
    public class AcceptedProblemModel {
        public Guid Id { get; set; }
        public int ProblemNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public ProblemDifficulty Difficulty { get; set; }
        public DateTime AcceptedAt { get; set; }
    }
}
