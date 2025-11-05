using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Exceptions;
using dotnetBitSmith.Interfaces;
using Microsoft.EntityFrameworkCore;
using dotnetBitSmith.Models.Solutions;
using Microsoft.AspNetCore.Authorization;

namespace dotnetBitSmith.Services;

public class SolutionService : ISolutionService {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SolutionService> _logger;

    public SolutionService(ApplicationDbContext context, ILogger<SolutionService> logger) {
        _context = context;
        _logger = logger;
    }
    public async Task<SolutionSummaryModel> CreateSolutionAsync(SolutionCreateModel model, Guid userId) {
        _logger.LogInformation("User {UserId} attempting to create solution to Problem {ProblemId}", userId, model.ProblemId);

        var problemExists = await _context.Problems.AnyAsync(p => p.Id == model.ProblemId);
        if (!problemExists) {
            _logger.LogWarning("User {UserId} failed to submit to non-existent Problem {ProblemId}", userId, model.ProblemId);
            throw new NotFoundException("Problem with ID " + model.ProblemId + " not found.");
        }

        var newSolution = new Solution {
            Id = Guid.NewGuid(),
            Title = model.Title,
            Content = model.Content,
            CreatedAt = DateTime.UtcNow,
            ProblemId = model.ProblemId,
            UserId = userId
        };

        await _context.Solutions.AddAsync(newSolution);
        await _context.SaveChangesAsync();

        var user = await _context.Users.FindAsync(userId);
        var username = user != null ? (user.DisplayName ?? user.Username) : "Unknown";
        _logger.LogInformation("Successfully created new solution{SolutionId} for problem with Id: {ProblemId} by user {UserId}", newSolution.Id, model.ProblemId, userId);

        return new SolutionSummaryModel {
            Id = newSolution.Id,
            ProblemId = newSolution.ProblemId,
            Title = newSolution.Title,
            AuthorName = username,
            VoteCount = 0,
            CreatedAt = newSolution.CreatedAt
        };
    }

    public async Task<IEnumerable<SolutionSummaryModel>> GetSolutionsForProblemAsync(Guid problemId) {
        _logger.LogInformation("Fetching solutions for problem with Id {ProblemId}", problemId);

        var problemExists = await _context.Problems.AnyAsync(p => p.Id == problemId);
        if (!problemExists) {
            _logger.LogWarning("Failed to fetch solution to non-existent Problem {ProblemId}", problemId);
            throw new NotFoundException("Problem with ID " + problemId + " not found.");
        }

        var solutions = await _context.Solutions
            .AsNoTracking()
            .Where(s => s.ProblemId == problemId)
            .OrderByDescending(s => s.CreatedAt) //Order by newest first
            .Select(
                s => new SolutionSummaryModel {
                    Id = s.Id,
                    ProblemId = problemId,
                    Title = s.Title,
                    AuthorName = s.User != null ? (s.User.DisplayName ?? s.User.Username) : "Unknown",
                    VoteCount = s.Votes.Count,
                    CreatedAt = s.CreatedAt
                }
            ).ToListAsync();

        return solutions;
    }
}