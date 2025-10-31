using System;
using System.Threading.Tasks;
using dotnetBitSmith.Services;
using System.Collections.Generic;
using dotnetBitSmith.Models.Problems;

namespace dotnetBitSmith.Interfaces {
    public interface IProblemService {
        Task<IEnumerable<ProblemSummaryModel>> GetProblemsAsync();
        Task<ProblemDetailModel> GetProblemByIdAsync(Guid problemId);
    }
}