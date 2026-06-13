using dotnetBitSmith.Entities;
using dotnetBitSmith.Models.Submissions;

namespace dotnetBitSmith.Interfaces {
    public interface ICompilationService {
        /// <summary>
        /// Takes a "Pending" submission, sends it to the judge,
        /// polls for a result, and updates the submission entity.
        /// </summary>
        /// <param name="submission"></param>
        /// <returns>updated submission entity with the final status, time and, memory</returns>
        Task<Submission> JudgeSubmissionAsync(Submission submission);
        Task<SampleRunResultModel> RunSampleAsync(string language, string code, TestCase testCase, string problemTitle, string problemDescription);
        Task<IEnumerable<SampleRunResultModel>> RunSamplesAsync(string language, string code, List<TestCase> testCases, string problemTitle, string problemDescription);
        Task<RunCodeResultModel> ExecuteCustomCodeAsync(string language, string code, string stdin);
    }
}
