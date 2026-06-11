using dotnetBitSmith.Models.Submissions;

namespace dotnetBitSmith.Interfaces {
    public interface ISubmissionService {
        Task<SubmissionResultModel> CreateSubmissionAsync(SubmissionCreateModel model, Guid userId);
        Task<IEnumerable<SubmissionDetailModel>> GetMySubmissionsForProblemAsync(Guid problemId, Guid userId);
        Task<IEnumerable<SampleRunResultModel>> RunSampleTestsAsync(SampleRunRequestModel model, Guid userId);
        Task<RunCodeResultModel> RunCustomCodeAsync(RunCodeRequestModel model, Guid userId);
    }
}
