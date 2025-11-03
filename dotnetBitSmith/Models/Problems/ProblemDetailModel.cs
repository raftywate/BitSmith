using dotnetBitSmith.Entities.Enums;

namespace dotnetBitSmith.Models.Problems {
    public class ProblemDetailModel {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }     
        public ProblemDifficulty Difficulty { get; set; }
        public string? StarterCode { get; set; }
        public List<CategoryModel> Categories { get; set; } = new();
        public string AuthorName { get; set; }
    }
}