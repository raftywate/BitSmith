using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Exceptions;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using dotnetBitSmith.Models.Submissions;

namespace dotnetBitSmith.Services {
    public class SubmissionService : ISubmissionService {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SubmissionService> _logger;
        private readonly ICompilationService _compilationService;
        private readonly ISubmissionQueue _queue;

        public SubmissionService(
            ApplicationDbContext context, 
            ILogger<SubmissionService> logger, 
            ICompilationService compilationService,
            ISubmissionQueue queue) {
            _context = context;
            _logger = logger;
            _compilationService = compilationService;
            _queue = queue;
        }

        public async Task<SubmissionResultModel> CreateSubmissionAsync(SubmissionCreateModel model, Guid userId) {
            _logger.LogInformation("User {UserId} attempting to submit to Problem {ProblemId}", userId, model.ProblemId);

            var problemExists = await _context.Problems.AnyAsync(p => p.Id == model.ProblemId);
            if (!problemExists) {
                _logger.LogWarning("User {UserId} failed to submit to non-existent Problem {ProblemId}", userId, model.ProblemId);
                throw new NotFoundException("Problem with ID " + model.ProblemId + " not found.");
            }

            var normalizedLang = model.Language?.Trim().ToLower() ?? string.Empty;
            if (normalizedLang == "c#") normalizedLang = "csharp";

            var newSubmission = new Submission {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProblemId = model.ProblemId,
                Status = SubmissionStatus.Pending,
                Language = normalizedLang,
                Code = model.Code,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Submissions.AddAsync(newSubmission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Submission {SubmissionId} created successfully. Enqueueing for background processing.", newSubmission.Id);
            
            await _queue.QueueSubmissionAsync(newSubmission.Id);

            return new SubmissionResultModel {
                Id = newSubmission.Id,
                ProblemId = newSubmission.ProblemId,
                Status = newSubmission.Status,
                ErrorMessage = newSubmission.ErrorMessage,
                CreatedAt = newSubmission.CreatedAt,
                PassedCount = newSubmission.PassedCount,
                TotalCount = newSubmission.TotalCount,
                FailedTestCaseInput = newSubmission.FailedTestCaseInput,
                FailedTestCaseExpected = newSubmission.FailedTestCaseExpected,
                FailedTestCaseActual = newSubmission.FailedTestCaseActual
            };
        }

        public async Task<IEnumerable<SubmissionDetailModel>> GetMySubmissionsForProblemAsync(Guid problemId, Guid userId) {
            _logger.LogInformation("User {UserId} attempting to access to Submissions for Id {ProblemId}", userId, problemId);
            
            var submissions = await _context.Submissions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.ProblemId == problemId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(
                s => new SubmissionDetailModel {
                    Id = s.Id,
                    ProblemId = s.ProblemId,
                    Code = s.Code,
                    Language = s.Language,
                    Status = s.Status,
                    ExecutionTimeMs = s.ExecutionTimeMs,
                    ExecutionMemoryKb = s.ExecutionMemoryKb,
                    ErrorMessage = s.ErrorMessage,
                    CreatedAt = s.CreatedAt,
                    PassedCount = s.PassedCount,
                    TotalCount = s.TotalCount,
                    FailedTestCaseInput = s.FailedTestCaseInput,
                    FailedTestCaseExpected = s.FailedTestCaseExpected,
                    FailedTestCaseActual = s.FailedTestCaseActual
                })
            .ToListAsync();

            foreach(var sub in submissions) {
                sub.CreatedAt = DateTime.SpecifyKind(sub.CreatedAt, DateTimeKind.Utc);
            }

            return submissions;
        }

        public async Task<IEnumerable<SampleRunResultModel>> RunSampleTestsAsync(SampleRunRequestModel model, Guid userId) {
            _logger.LogInformation("User {UserId} running sample tests for Problem {ProblemId}", userId, model.ProblemId);

            var problem = await _context.Problems
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == model.ProblemId);

            if (problem == null) {
                _logger.LogWarning("User {UserId} failed to run sample tests for non-existent Problem {ProblemId}", userId, model.ProblemId);
                throw new NotFoundException("Problem with ID " + model.ProblemId + " not found.");
            }

            var testCasesList = await _context.TestCases
                .AsNoTracking()
                .Where(testCase => testCase.ProblemId == model.ProblemId && !testCase.IsHidden)
                .ToListAsync();
                
            var testCases = testCasesList.OrderBy(testCase => testCase.Id).ToList();

            if (!testCases.Any()) {
                return Enumerable.Empty<SampleRunResultModel>();
            }

            var normalizedLang = model.Language?.Trim().ToLower() ?? string.Empty;
            if (normalizedLang == "c#") normalizedLang = "csharp";

            var problemTitle = problem.Title;
            var problemDesc = problem.Description;

            return await _compilationService.RunSamplesAsync(normalizedLang, model.Code, testCases, problemTitle, problemDesc);
        }

        public async Task<RunCodeResultModel> RunCustomCodeAsync(RunCodeRequestModel model, Guid userId) {
            _logger.LogInformation("User {UserId} running custom code run", userId);
            
            var normalizedLang = model.Language?.Trim().ToLower() ?? string.Empty;
            if (normalizedLang == "c#") normalizedLang = "csharp";

            return await _compilationService.ExecuteCustomCodeAsync(normalizedLang, model.Code, model.CustomStdin);
        }

        public async Task<SubmissionDetailModel?> GetSubmissionByIdAsync(Guid id, Guid userId) {
            _logger.LogInformation("User {UserId} attempting to access submission {SubmissionId}", userId, id);
            
            var submission = await _context.Submissions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (submission == null) {
                return null;
            }

            return new SubmissionDetailModel {
                Id = submission.Id,
                ProblemId = submission.ProblemId,
                Code = submission.Code,
                Language = submission.Language,
                Status = submission.Status,
                ExecutionTimeMs = submission.ExecutionTimeMs,
                ExecutionMemoryKb = submission.ExecutionMemoryKb,
                ErrorMessage = submission.ErrorMessage,
                CreatedAt = DateTime.SpecifyKind(submission.CreatedAt, DateTimeKind.Utc),
                PassedCount = submission.PassedCount,
                TotalCount = submission.TotalCount,
                FailedTestCaseInput = submission.FailedTestCaseInput,
                FailedTestCaseExpected = submission.FailedTestCaseExpected,
                FailedTestCaseActual = submission.FailedTestCaseActual
            };
        }
    }
}
