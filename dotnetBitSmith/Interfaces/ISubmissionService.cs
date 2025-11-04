using dotnetBitSmith.Models.Submissions;

namespace dotnetBitSmith.Interfaces {
    public interface ISubmissionService {
        Task<SubmissionResultModel> CreateSubmissionAsync(SubmissionCreateModel model, Guid userId);
    }
}