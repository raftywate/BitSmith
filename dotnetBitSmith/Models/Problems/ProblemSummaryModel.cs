using dotnetBitSmith.Entities.Enums;

namespace dotnetBitSmith.Models.Problems {
    public class ProblemSummaryModel {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public ProblemDifficulty ProblemDifficulty { get; set; }
        public List<CategoryModel> Categories { get; set; } = new();
    }
}