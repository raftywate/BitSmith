using dotnetBitSmith.Entities;
using dotnetBitSmith.Models.Comments;

namespace dotnetBitSmith.Interfaces {
    public interface ICommentService {
        Task<CommentViewModel> CreateCommentAsync(CommentCreateModel model, Guid userId);
        Task<IEnumerable<CommentViewModel>> GetCommentsForSolutionAsync(Guid solutionId, CommentParametersModel parameters);
    } 
}