using dotnetBitSmith.Models.Problems;

namespace dotnetBitSmith.Interfaces {
    public interface IProblemService {
        /// Gets a lightweight list of all problems for the summary page.
        Task<ProblemSummaryListModel> GetProblemsAsync(ProblemParametersModel parameters);
        /// Gets the full details for a single problem by its ID.
        Task<ProblemDetailModel> GetProblemByIdAsync(Guid problemId);
        /// Creates a new problem in the database.
        Task<ProblemDetailModel> CreateProblemAsync(ProblemCreateModel model, Guid authorId);
        /// Updates an existing problem in the database.
        Task<ProblemDetailModel> UpdateProblemAsync(Guid problemId, ProblemUpdateModel model);
        /// Gets all categories for filtering/admin.
        Task<IEnumerable<CategoryModel>> GetAllCategoriesAsync();
        /// Adds test cases to an existing problem.
        Task<ProblemDetailModel> AddTestCasesAsync(Guid problemId, List<TestCaseCreateModel> testCases);
        /// Replaces all test cases on an existing problem.
        Task<ProblemDetailModel> ReplaceTestCasesAsync(Guid problemId, List<TestCaseCreateModel> testCases);
    }
}
