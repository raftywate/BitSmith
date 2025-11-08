using dotnetBitSmith.Models.Votes;

namespace dotnetBitSmith.Interfaces {
    public interface IVoteService {
        Task<int> CastVoteAsync(VoteModel model, Guid userId);
    }
}