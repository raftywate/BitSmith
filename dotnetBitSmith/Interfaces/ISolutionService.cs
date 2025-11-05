using dotnetBitSmith.Models.Solutions;

namespace dotnetBitSmith.Interfaces {
    public interface ISolutionService {
        Task<SolutionSummaryModel> CreateSolutionAsync(SolutionCreateModel model, Guid userId);
        Task<IEnumerable<SolutionSummaryModel>> GetSolutionsForProblemAsync(Guid problemId);
    }
}