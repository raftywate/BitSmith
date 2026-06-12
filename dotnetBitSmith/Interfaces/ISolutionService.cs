using dotnetBitSmith.Models.Solutions;

namespace dotnetBitSmith.Interfaces {
    public interface ISolutionService {
        Task<SolutionSummaryModel> CreateSolutionAsync(SolutionCreateModel model, Guid userId);
        Task<IEnumerable<SolutionSummaryModel>> GetSolutionsForProblemAsync(Guid problemId);
        Task<IEnumerable<SolutionSummaryModel>> GetSolutionsByUserAsync(Guid userId);
        Task<SolutionDetailModel> GetSolutionByIdAsync(Guid solutionId);
        Task<SolutionDetailModel> UpdateSolutionAsync(Guid id, Guid userId, SolutionUpdateModel model);
        Task DeleteSolutionAsync(Guid id, Guid userId, bool isAdmin = false);
    }
}
