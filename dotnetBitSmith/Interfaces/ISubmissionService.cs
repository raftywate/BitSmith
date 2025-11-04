using dotnetBitSmith.Models.Submissions;

namespace dotnetBitSmith.Interfaces {
    public interface ISubmissionService {
        Task<SubmissionResultModel> CreateSubmissionAsync(SubmissionCreateModel model, Guid userId);
        Task<IEnumerable<SubmissionDetailModel>> GetMySubmissionsForProblemAsync(Guid problemId, Guid userId);
    }
}