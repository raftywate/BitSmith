using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Exceptions;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Comments;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace dotnetBitSmith.Services {
    public class CommentService : ICommentService {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CommentService> _logger;

        public CommentService(ApplicationDbContext context, ILogger<CommentService> logger) {
            _context = context;
            _logger = logger;
        }

        public async Task<CommentViewModel> CreateCommentAsync(CommentCreateModel model, Guid userId) {
            _logger.LogInformation("User {UserId} creating comment for Solution {SolutionId}", userId, model.SolutionId);

            var solutionExists = await _context.Solutions.AnyAsync(s => s.Id == model.SolutionId);
            if (!solutionExists) {
                throw new NotFoundException("Solution with Id {model.SolutionId} not found");
            }

            if (model.ParentCommentId.HasValue) {
                // Checking that the current comment that is also the parent comment not only exists, but is for the *same solution*
                if (!await _context.Comments.AnyAsync(c => c.Id == model.ParentCommentId.Value && c.SolutionId == model.SolutionId)) {
                    throw new NotFoundException($"Parent comment with ID {model.ParentCommentId.Value} not found for this solution.");
                }
            }
            
            var newComment = new Comment {
                Id = Guid.NewGuid(),
                Content = model.Content,
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
                SolutionId = model.SolutionId,
                ParentCommentId = model.ParentCommentId
            };

            await _context.Comments.AddAsync(newComment);
            await _context.SaveChangesAsync();

            var user = await _context.Users.FindAsync(userId);
            var username = user != null ? (user.DisplayName ?? user.Username) : "Unknown";
            // var VoteCount = await _context.Votes.FindAsync();

            return new CommentViewModel {
                Id = newComment.Id,
                SolutionId = newComment.SolutionId,
                ParentCommentId = newComment.ParentCommentId,
                Content = newComment.Content,
                AuthorUsername = username,
                CreatedAt = newComment.CreatedAt,
                VoteCount = 0 // new comments have zero votes
            };
        }

        public async Task<IEnumerable<CommentViewModel>> GetCommentsForSolutionAsync(Guid solutionId) {
            _logger.LogInformation("Fetching comment tree for Solution {SolutionId}", solutionId);

            // Fetch ALL comments for this solution in ONE query ---
            // Include User for the username, and Votes for the count.

            var allComments = await _context.Comments
                .Include(c => c.User) //user names needed
                .Include(c => c.Votes)
                .Where(c => c.SolutionId == solutionId)
                .OrderBy(c => c.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            var commentLookup = new Dictionary<Guid, CommentViewModel>();
            var allCommentsViewModels = new List<CommentViewModel>();

            foreach (var comment in allComments) {
                var commentVM = new CommentViewModel {
                    Id = comment.Id,
                    Content = comment.Content,
                    CreatedAt = comment.CreatedAt,
                    SolutionId = comment.SolutionId,
                    ParentCommentId = comment.ParentCommentId,
                    VoteCount = comment.Votes.Count(v => v.IsUpvote) - comment.Votes.Count(v => !v.IsUpvote),
                    AuthorUsername = comment.User != null ? (comment.User.DisplayName ?? comment.User.Username) : "Unknown"
                    // Replies list is still empty, which is correct for now
                };

                allCommentsViewModels.Add(commentVM);
                commentLookup.Add(commentVM.Id, commentVM);
            }

            // Building the "Tree" (The "Final Report")`
            // This is the list will be actually returned.

            var nestedComments = new List<CommentViewModel>();
            foreach(var commentVM in allCommentsViewModels) { 
                if(commentVM.ParentCommentId == null) {
                    nestedComments.Add(commentVM);
                } else {
                    if(commentLookup.TryGetValue(commentVM.ParentCommentId.Value, out var parentComment)) {
                        parentComment.Replies.Add(commentVM);
                    }
                }
            }

           return nestedComments;
        }
    }
}