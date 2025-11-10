using dotnetBitSmith.Entities;

namespace dotnetBitSmith.Interfaces {
    public interface ICompilationService {
        /// <summary>
        /// Takes a "Pending" submission, sends it to the judge,
        /// polls for a result, and updates the submission entity.
        /// </summary>
        /// <param name="submission"></param>
        /// <returns>updated submission entity with the final status, time and, memory</returns>
        Task<Submission> JudgeSubmissionAsync(Submission submission);
    }
}