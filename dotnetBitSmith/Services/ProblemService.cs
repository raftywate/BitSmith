using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Exceptions;
using dotnetBitSmith.Interfaces;
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

        public async Task<IEnumerable<ProblemSummaryModel>> GetProblemsAsync() {
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
                    Categories = p.ProblemCategories.Select(pc => new CategoryModel {
                        Id = pc.Category != null ? pc.Category.Id : Guid.Empty,
                        Name = pc.Category != null ? pc.Category.Name : "Unknown",
                        Slug = pc.Category != null ? pc.Category.Slug : "unknown"
                    }).ToList()
                })
                .ToListAsync();
            return problems;
        }

        public async Task<ProblemDetailModel> GetProblemByIdAsync(Guid problemId) {
            _logger.LogInformation("Fetching problem details for Id {ProblemId}", problemId);

            var problem = await _context.Problems
               .AsNoTracking()
               .Where(p => p.Id == problemId)
               .Select(p => new ProblemDetailModel
               {
                   Id = p.Id,
                   Title = p.Title,
                   Description = p.Description,
                   Difficulty = p.Difficulty,
                   StarterCode = p.StarterCode,
                   AuthorName = p.Author != null ? (p.Author.DisplayName ?? p.Author.Username) : "Unknown",
                   Categories = p.ProblemCategories.Select(pc => new CategoryModel
                   {
                       Id = pc.Category != null ? pc.Category.Id : Guid.Empty,
                        Name = pc.Category != null ? pc.Category.Name : "Unknown",
                        Slug = pc.Category != null ? pc.Category.Slug : "unknown"
                   }).ToList()
               }).FirstOrDefaultAsync();

            return problem;
        }
        
        public async Task<ProblemDetailModel> CreateProblemAsync(ProblemCreateModel model, Guid authorId) {
            _logger.LogInformation("Attempting to create a new problem with Title: {Title}", model.Title);

            ///Starting a databse transaction that ensures that if *any* part of the creation fails,
            /// the entire operation is rolled back.
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try {
                //Creating the base problem entity from the model that was sent from the frontend
                var problem = new Problem {
                    Id = Guid.NewGuid(),
                    Title = model.Title,
                    Description = model.Description,
                    Difficulty = model.Difficulty,
                    StarterCode = model.StarterCode,
                    AuthorId = authorId,
                    CreatedAt = DateTime.UtcNow
                };

                //Checking if the author has ticked the categories the problem belongs to
                if (model.CategoryIDs != null && model.CategoryIDs.Any()) {
                    //finding all categories that was sent in the model that actually exist in the DB
                    var categories = await _context.Categories
                    .Where(c => model.CategoryIDs.Contains(c.Id))
                    .ToListAsync();

                    //if they sent an invalid category Id, throw an error.
                    if (categories.Count != model.CategoryIDs.Count) {
                        var foundIds = categories.Select(c => c.Id);
                        var notFoundIds = string.Join(", ", model.CategoryIDs.Except(foundIds));
                        _logger.LogWarning("Failed to create problem. Invalid Category IDs: {CategoryIds} ", notFoundIds);
                        throw new NotFoundException("One or more categories not found. Invalid IDs: " + notFoundIds);
                    }

                    //Ensuring we update the ProblemCategories list corressponding to the current problem
                    foreach (var category in categories) {
                        problem.ProblemCategories.Add(new ProblemCategory {
                            ProblemId = problem.Id,
                            CategoryId = category.Id
                        });
                    }
                }

                _context.Problems.Add(problem);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Successfully created new problem with Id: {ProblemId}" + problem.Id);

                return await GetProblemByIdAsync(problem.Id);
            } catch(Exception ex) {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create new problem. Transaction rolled back.");
                ///preserves the entire stack trace, unlike throw ex that resets the "stack trace." 
                ///The error log the middleware receives will say the error started in ProblemService.cs,
                ///and it will have no idea where the error originally came from (e.g., deep inside the database driver).
                ///Our ExceptionHandlingMiddleware will get the exception and be able to see the full story: 
                ///"The error started in the database, bubbled up to ProblemService (which rolled back), and then bubbled up to me.
                throw;
            }
        }
    }
}