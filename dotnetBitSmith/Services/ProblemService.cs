using System;
using System.Text;
using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Exceptions;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Middleware;
using dotnetBitSmith.Models.Problems;
using Microsoft.EntityFrameworkCore;

namespace dotnetBitSmith.Services {
    public class ProblemService : IProblemService {

        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProblemService> _logger;

        public ProblemService(ApplicationDbContext context, ILogger<ProblemService> logger) {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<ProblemSummaryModel>> GetProblemsAsync()
        {
            _logger.LogInformation("Fetching all problem summaries.");
            // This is a LINQ Projection Query.
            // We use .Select() to map *directly* from the Entity to the DTO.
            // This is extremely efficient. EF Core writes SQL to only
            // select the columns we need.
            var problems = await _context.Problems
                .AsNoTracking()
                .Select(p => new ProblemSummaryModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    ProblemDifficulty = p.Difficulty,
                    Categories = p.ProblemCategories.Select(pc => new CategoryModel
                    {
                        Id = pc.Category.Id,
                        Name = pc.Category.Name,
                        Slug = pc.Category.Slug
                    }).ToList()
                })
                .ToListAsync();
            return problems;
        }
        
        public async Task<ProblemDetailModel> GetProblemByIdAsync(Guid problemId) {
            _logger.LogInformation("Fetching problem details for the Id{ProblemId}" + problemId);

            var problem = await _context.Problems
               .AsNoTracking()
               .Where(p => p.Id == problemId)
               .Select(p => new ProblemDetailModel {
                   Id = p.Id,
                   Title = p.Title,
                   Description = p.Description,
                   Difficulty = p.Difficulty,
                   StarterCode = p.StarterCode,
                   AuthorName = p.Author.DisplayName ?? p.Author.Username,
                   Categories = p.ProblemCategories.Select(pc => new CategoryModel {
                       Id = pc.Category.Id,
                       Name = pc.Category.Name,
                       Slug = pc.Category.Slug
                   }).ToList()
               }).FirstOrDefaultAsync();

            return problem;
        }
    }
}