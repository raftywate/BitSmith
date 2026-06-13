using System;
using System.Threading;
using System.Threading.Tasks;
using dotnetBitSmith.Data;
using dotnetBitSmith.Entities.Enums;
using dotnetBitSmith.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace dotnetBitSmith.Services {
    public class SubmissionProcessingWorker : BackgroundService {
        private readonly ISubmissionQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubmissionProcessingWorker> _logger;

        public SubmissionProcessingWorker(
            ISubmissionQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<SubmissionProcessingWorker> logger) {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            _logger.LogInformation("SubmissionProcessingWorker is starting...");

            while (!stoppingToken.IsCancellationRequested) {
                try {
                    var submissionId = await _queue.DequeueSubmissionAsync(stoppingToken);
                    _logger.LogInformation("Dequeued submission {SubmissionId} for processing.", submissionId);

                    await ProcessSubmissionAsync(submissionId, stoppingToken);
                } catch (OperationCanceledException) {
                    _logger.LogInformation("SubmissionProcessingWorker received cancel request; shutting down.");
                    break;
                } catch (Exception ex) {
                    _logger.LogError(ex, "An unhandled exception occurred in the execution loop of SubmissionProcessingWorker.");
                }
            }
        }

        private async Task ProcessSubmissionAsync(Guid submissionId, CancellationToken cancellationToken) {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var compilationService = scope.ServiceProvider.GetRequiredService<ICompilationService>();

            var submission = await context.Submissions.FindAsync(new object[] { submissionId }, cancellationToken);
            if (submission == null) {
                _logger.LogWarning("Submission {SubmissionId} was queued but not found in the database.", submissionId);
                return;
            }

            // Immediately mark as Running so frontend/user sees progress
            submission.Status = SubmissionStatus.Running;
            context.Submissions.Update(submission);
            await context.SaveChangesAsync(cancellationToken);

            try {
                _logger.LogInformation("Judging submission {SubmissionId} (Language: {Language}).", submission.Id, submission.Language);
                await compilationService.JudgeSubmissionAsync(submission);
                _logger.LogInformation("Finished judging submission {SubmissionId}.", submission.Id);
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to compile/judge submission {SubmissionId}.", submission.Id);
                
                try {
                    // Fallback to update database with InternalError so the submission is not stuck in Running
                    submission.Status = SubmissionStatus.InternalError;
                    submission.ErrorMessage = "Internal error during background compilation: " + ex.Message;
                    context.Submissions.Update(submission);
                    await context.SaveChangesAsync(cancellationToken);
                } catch (Exception dbEx) {
                    _logger.LogError(dbEx, "Failed to save fallback error status for submission {SubmissionId}.", submission.Id);
                }
            }
        }
    }
}
