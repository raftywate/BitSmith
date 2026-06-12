using dotnetBitSmith.Entities;
using dotnetBitSmith.Models.Comments;

namespace dotnetBitSmith.Interfaces {
    public interface ICommentService {
        Task<CommentViewModel> CreateCommentAsync(CommentCreateModel model, Guid userId);
        Task<IEnumerable<CommentViewModel>> GetCommentsForSolutionAsync(Guid solutionId, CommentParametersModel parameters);
        Task<CommentViewModel> UpdateCommentAsync(Guid id, Guid userId, CommentUpdateModel model);
        Task DeleteCommentAsync(Guid id, Guid userId, bool isAdmin = false);
    } 
}