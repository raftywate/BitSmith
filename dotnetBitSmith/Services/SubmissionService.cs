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

        public SubmissionService(ApplicationDbContext context, ILogger<SubmissionService> logger, ICompilationService compilationService) {
            _context = context;
            _logger = logger;
            _compilationService = compilationService;
        }

        public async Task<SubmissionResultModel> CreateSubmissionAsync(SubmissionCreateModel model, Guid userId) {
            _logger.LogInformation("User {UserId} attempting to submit to Problem {ProblemId}", userId, model.ProblemId);

            var problemExists = await _context.Problems.AnyAsync(p => p.Id == model.ProblemId);
            if (!problemExists) {
                _logger.LogWarning("User {UserId} failed to submit to non-existent Problem {ProblemId}", userId, model.ProblemId);
                throw new NotFoundException("Problem with ID " + model.ProblemId + " not found.");
            }

            var newSubmission = new Submission {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProblemId = model.ProblemId,
                Status = SubmissionStatus.Pending,
                Language = model.Language,
                Code = model.Code,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Submissions.AddAsync(newSubmission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Submission {SubmissionId} created successfully.", newSubmission.Id);
            
            try {
                newSubmission = await _compilationService.JudgeSubmissionAsync(newSubmission);
            } catch(Exception ex) {
                _logger.LogError(ex, "Judge failed for Submission {SubmissionID}. Setting to InternalError.", newSubmission.Id);
                // The Judge service already handles setting the error state,
                // but we catch it here just in case the service itself blows up.
                newSubmission.Status = SubmissionStatus.InternalError;
            }

            _logger.LogInformation("Submission {SubmissionId} judged with status: {Status}", newSubmission.Id, newSubmission.Status);

            return new SubmissionResultModel {
                Id = newSubmission.Id,
                ProblemId = newSubmission.ProblemId,
                Status = newSubmission.Status,
                CreatedAt = newSubmission.CreatedAt
            };
        }

        public async Task<IEnumerable<SubmissionDetailModel>> GetMySubmissionsForProblemAsync(Guid problemId, Guid userId) {
            _logger.LogInformation("User {UserId} attempting to access to Submissions for Id {ProblemId}", userId, problemId);
            
            var submissions = await _context.Submissions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.ProblemId == problemId)
            .Select(
                s => new SubmissionDetailModel {
                    Id = s.Id,
                    Code = s.Code,
                    Language = s.Language,
                    Status = s.Status,
                    ExecutionTimeMs = s.ExecutionTimeMs,
                    ExecutionMemoryKb = s.ExecutionMemoryKb,
                    CreatedAt = s.CreatedAt
                })
            .ToListAsync();

            return submissions;
        }
    }
}