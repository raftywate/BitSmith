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

        public async Task<IEnumerable<CommentViewModel>> GetCommentsForSolutionAsync(Guid solutionId, CommentParametersModel parameters) {
            _logger.LogInformation("Fetching comments for Solution {SolutionId}, Page {Page}, Size {Size}", solutionId, parameters.PageNumber, parameters.PageSize);

            // Fetch ALL comments for this solution in ONE query ---
            // Include User for the username, and Votes for the count.

            if (!await _context.Solutions.AnyAsync(s => s.Id == solutionId)) {
                throw new NotFoundException($"Solution with ID {solutionId} not found.");
            }

            // 1. Get the IDs of the ROOT comments for this page
            var pagedRootIds = await _context.Comments
                .AsNoTracking()
                .Where(c => c.SolutionId == solutionId && c.ParentCommentId == null) // Only roots
                .OrderByDescending(c => c.CreatedAt)
                .Skip((parameters.PageNumber - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .Select(c => c.Id)
                .ToListAsync();


            // 2. Fetch ALL comments (roots + replies) that are part of these threads
            // Ideally, we'd use a recursive CTE here for infinite depth, but for V1, 
            // fetching all comments for the solution and filtering in memory is efficient enough for moderate sizes.
            var allComments = await _context.Comments
                .Include(c => c.User)
                .Include(c => c.Votes)
                .Where(c => c.SolutionId == solutionId) // Get everything for the solution
                .OrderBy(c => c.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            // 3. Build the Lookup
            var commentLookup = new Dictionary<Guid, CommentViewModel>();
            var allCommentViewModels = new List<CommentViewModel>();

            foreach (var comment in allComments) {
                var commentVM = new CommentViewModel {
                    Id = comment.Id,
                    SolutionId = comment.SolutionId,
                    ParentCommentId = comment.ParentCommentId,
                    Content = comment.Content,
                    AuthorUsername = comment.User != null ? (comment.User.DisplayName ?? comment.User.Username) : "Unknown",
                    CreatedAt = comment.CreatedAt,
                    VoteCount = comment.Votes.Count(v => v.IsUpvote) - comment.Votes.Count(v => !v.IsUpvote)
                };
                allCommentViewModels.Add(commentVM);
                commentLookup.Add(commentVM.Id, commentVM);
            }

            // 4. Build Tree & Filter
            var pagedResults = new List<CommentViewModel>();

            foreach (var commentVM in allCommentViewModels) {
                if (commentVM.ParentCommentId == null) {
                    // Only add this root if it was in our paged list of IDs
                    if (pagedRootIds.Contains(commentVM.Id)) {
                        pagedResults.Add(commentVM);
                    }
                }
                else {
                    // Always attach children to their parents
                    if (commentLookup.TryGetValue(commentVM.ParentCommentId.Value, out var parentComment)) {
                        parentComment.Replies.Add(commentVM);
                    }
                }
            }

            // Re-sort the final list to ensure correct page order
            return pagedResults.OrderByDescending(c => c.CreatedAt);
        }

    }
}