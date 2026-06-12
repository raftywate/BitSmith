using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Exceptions;
using dotnetBitSmith.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        var authorUsername = user != null ? user.Username : "Unknown";
        var authorDisplayName = user != null ? (user.DisplayName ?? user.Username) : "Unknown";
        _logger.LogInformation("Successfully created new solution{SolutionId} for problem with Id: {ProblemId} by user {UserId}", newSolution.Id, model.ProblemId, userId);

        return new SolutionSummaryModel {
            Id = newSolution.Id,
            ProblemId = newSolution.ProblemId,
            Title = newSolution.Title,
            AuthorName = authorDisplayName,
            AuthorUsername = authorUsername,
            Excerpt = BuildExcerpt(newSolution.Content),
            VoteCount = 0,
            CommentCount = 0,
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
                    AuthorUsername = s.User != null ? s.User.Username : "Unknown",
                    Excerpt = BuildExcerpt(s.Content),
                    VoteCount = _context.Votes.Count(v => v.EntityId == s.Id && v.IsUpvote) - _context.Votes.Count(v => v.EntityId == s.Id && !v.IsUpvote),
                    CommentCount = s.Comments.Count,
                    CreatedAt = s.CreatedAt
                }
            ).ToListAsync();

        return solutions;
    }

    public async Task<IEnumerable<SolutionSummaryModel>> GetSolutionsByUserAsync(Guid userId) {
        _logger.LogInformation("Fetching solutions for user with Id {UserId}", userId);

        var query = await _context.Solutions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(
                s => new {
                    s.Id,
                    s.ProblemId,
                    s.Title,
                    AuthorName = s.User != null ? (s.User.DisplayName ?? s.User.Username) : "Unknown",
                    AuthorUsername = s.User != null ? s.User.Username : "Unknown",
                    s.Content,
                    VoteCount = _context.Votes.Count(v => v.EntityId == s.Id && v.IsUpvote) - _context.Votes.Count(v => v.EntityId == s.Id && !v.IsUpvote),
                    CommentCount = s.Comments.Count,
                    s.CreatedAt,
                    ProblemTitle = s.Problem != null ? s.Problem.Title : null,
                    ProblemNumber = s.Problem != null ? (int?)s.Problem.ProblemNumber : null
                }
            ).ToListAsync();

        var solutions = query.Select(s => new SolutionSummaryModel {
            Id = s.Id,
            ProblemId = s.ProblemId,
            Title = s.Title,
            AuthorName = s.AuthorName,
            AuthorUsername = s.AuthorUsername,
            Excerpt = BuildExcerpt(s.Content),
            VoteCount = s.VoteCount,
            CommentCount = s.CommentCount,
            CreatedAt = s.CreatedAt,
            ProblemTitle = s.ProblemTitle,
            ProblemNumber = s.ProblemNumber,
            ProblemSlug = s.ProblemTitle != null ? ProblemService.GenerateSlug(s.ProblemTitle) : null
        }).ToList();

        return solutions;
    }

    public async Task<SolutionDetailModel> GetSolutionByIdAsync(Guid solutionId) {
        _logger.LogInformation("Fetching solution detail for Id {SolutionId}", solutionId);

        var solution = await _context.Solutions
            .AsNoTracking()
            .Where(s => s.Id == solutionId)
            .Select(s => new SolutionDetailModel {
                Id = s.Id,
                ProblemId = s.ProblemId,
                Title = s.Title,
                AuthorName = s.User != null ? (s.User.DisplayName ?? s.User.Username) : "Unknown",
                AuthorUsername = s.User != null ? s.User.Username : "Unknown",
                Excerpt = BuildExcerpt(s.Content),
                Content = s.Content,
                VoteCount = _context.Votes.Count(v => v.EntityId == s.Id && v.IsUpvote) - _context.Votes.Count(v => v.EntityId == s.Id && !v.IsUpvote),
                CommentCount = s.Comments.Count,
                CreatedAt = s.CreatedAt
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException("Solution with ID " + solutionId + " not found.");

        return solution;
    }

    public async Task<SolutionDetailModel> UpdateSolutionAsync(Guid id, Guid userId, SolutionUpdateModel model) {
        var solution = await _context.Solutions.FindAsync(id) 
            ?? throw new NotFoundException("Solution with ID " + id + " not found.");

        if (solution.UserId != userId) {
            throw new UnauthorizedAccessException("You are not authorized to edit this solution.");
        }

        solution.Title = model.Title;
        solution.Content = model.Content;
        await _context.SaveChangesAsync();

        return await GetSolutionByIdAsync(id);
    }

    public async Task DeleteSolutionAsync(Guid id, Guid userId, bool isAdmin = false) {
        var solution = await _context.Solutions.FindAsync(id) 
            ?? throw new NotFoundException("Solution with ID " + id + " not found.");

        if (solution.UserId != userId && !isAdmin) {
            throw new UnauthorizedAccessException("You are not authorized to delete this solution.");
        }

        _context.Solutions.Remove(solution);
        await _context.SaveChangesAsync();
    }

    private static string BuildExcerpt(string content) {
        const int previewLength = 180;

        if (string.IsNullOrWhiteSpace(content)) {
            return string.Empty;
        }

        // Remove HTML tags
        var cleanContent = System.Text.RegularExpressions.Regex.Replace(content, "<[^>]*>", string.Empty);

        var trimmed = cleanContent.Trim();
        return trimmed.Length <= previewLength
            ? trimmed
            : trimmed[..previewLength] + "...";
    }
}
