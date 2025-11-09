namespace dotnetBitSmith.Models.Comments {
    public class CommentViewModel {
        public Guid Id { get; set; }
        public Guid SolutionId { get; set; }
        public Guid? ParentCommentId { get; set; }
        public string Content { get; set; }
        public string AuthorUsername { get; set; }
        public DateTime CreatedAt { get; set; }
        public int VoteCount { get; set; } = 0;
        public List<CommentViewModel> Replies { get; set; } = new List<CommentViewModel>();
    }
}