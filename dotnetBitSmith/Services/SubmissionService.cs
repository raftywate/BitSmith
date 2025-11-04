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

        public SubmissionService(ApplicationDbContext context, ILogger<SubmissionService> logger) {
            _context = context;
            _logger = logger;
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

            return new SubmissionResultModel {
                Id = newSubmission.Id,
                ProblemId = newSubmission.ProblemId,
                Status = newSubmission.Status,
                CreatedAt = newSubmission.CreatedAt
            };
        }
    }
}