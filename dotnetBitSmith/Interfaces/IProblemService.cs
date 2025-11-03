using dotnetBitSmith.Models.Problems;

namespace dotnetBitSmith.Interfaces {
    public interface IProblemService {
        /// Gets a lightweight list of all problems for the summary page.
        /// A collection of ProblemSummaryModel.
        Task<IEnumerable<ProblemSummaryModel>> GetProblemsAsync();
        /// Gets the full details for a single problem by its ID.
        /// A ProblemDetailModel or null if not found.
        Task<ProblemDetailModel> GetProblemByIdAsync(Guid problemId);

        /// Creates a new problem in the database.
        /// /// The full details of the newly created problem.
        Task<ProblemDetailModel> CreateProblemAsync(ProblemCreateModel model, Guid authorId);
    }
}