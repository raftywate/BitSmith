using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using Microsoft.AspNetCore.Mvc;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Votes;
using Microsoft.EntityFrameworkCore;

namespace dotnetBitSmith.Services {
    public class VoteService : IVoteService {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<VoteService> _logger;

        public VoteService(ApplicationDbContext context, ILogger<VoteService> logger) {
            _context = context;
            _logger = logger;
        }

        public async Task<int> CastVoteAsync(VoteModel model, Guid userId) {
            _logger.LogInformation("User {UserId} casting vote on Entity {EntityId}", userId, model.EntityId);

            var existingVote = await _context.Votes.FirstOrDefaultAsync(v => v.UserId == userId
            && v.EntityId == model.EntityId
            && v.EntityType == model.EntityType);

            if (existingVote == null) {
                //This is a new vote
                _logger.LogInformation("New vote (IsUpvote: {IsUpvote}) by User {UserId}", model.IsUpvote, userId);

                var newVote = new Vote {
                    Id = Guid.NewGuid(),
                    EntityId = model.EntityId,
                    EntityType = model.EntityType,
                    IsUpvote = model.IsUpvote,
                    UserId = userId
                };
                await _context.Votes.AddAsync(newVote);
            }
            else {
                // User is changing or removing their vote 
                if (existingVote.IsUpvote == model.IsUpvote) {
                    // User is "un-voting" (e.g., clicking upvote on an already upvoted item)
                    _logger.LogInformation("User {UserId} removing vote from Entity {EntityId}", userId, model.EntityId);

                    _context.Votes.Remove(existingVote);
                }
                else {
                    // User is "toggling" their vote (e.g., changing from downvote to upvote)
                    _logger.LogInformation("User {UserId} toggling vote on Entity {EntityId}", userId, model.EntityId);

                    existingVote.IsUpvote = model.IsUpvote;
                }
            }
            await _context.SaveChangesAsync();

            _logger.LogInformation("Calculating new vote count for Entity {EntityId}", model.EntityId);
            var upvoteCount   = await _context.Votes.CountAsync(v => v.EntityId == model.EntityId && v.IsUpvote);
            var downvoteCount = await _context.Votes.CountAsync(v => v.EntityId == model.EntityId && !v.IsUpvote);
            return (upvoteCount - downvoteCount);
        }
    }
}