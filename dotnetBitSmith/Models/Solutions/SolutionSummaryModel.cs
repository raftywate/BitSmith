namespace dotnetBitSmith.Models.Solutions {
    public class SolutionSummaryModel {
        public Guid Id { get; set; }
        public Guid ProblemId { get; set; }
        public string Title { get; set; }
        public string AuthorName { get; set; }
        public string Excerpt { get; set; }
        public int VoteCount { get; set; } = 0;
        public int CommentCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; }
    }
}
