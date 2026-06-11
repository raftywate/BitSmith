using dotnetBitSmith.Models.Problems;

namespace dotnetBitSmith.Interfaces {
    public interface IProblemService {
        /// Gets a lightweight list of all problems for the summary page.
        Task<ProblemSummaryListModel> GetProblemsAsync(ProblemParametersModel parameters);
        /// Gets the full details for a single problem by its ID.
        Task<ProblemDetailModel> GetProblemByIdAsync(Guid problemId, Guid? userId = null);
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
        /// Gets the problem of the day for a specific date (randomly selects if none).
        Task<ProblemSummaryModel> GetProblemOfTheDayAsync(DateOnly date);
        /// Sets a specific problem as the problem of the day for a date.
        Task<ProblemSummaryModel> SetProblemOfTheDayAsync(DateOnly date, Guid problemId);
        /// Gets the user's PoD activity.
        Task<PoDActivityModel> GetPoDActivityAsync(Guid userId, int tzOffsetMinutes, DateOnly todayLocal);
    }
}
