using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Exceptions;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Problems;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace dotnetBitSmith.Services {
    public class ProblemService : IProblemService {

        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProblemService> _logger;

        public ProblemService(ApplicationDbContext context, ILogger<ProblemService> logger) {
            _context = context;
            _logger = logger;
        }

        public async Task<ProblemSummaryListModel> GetProblemsAsync(ProblemParametersModel parameters) {
            _logger.LogInformation("Fetching problems page {PageNumber} with size {PageSize}", parameters.PageNumber, parameters.PageSize);

            // Build the base query with optional filters
            var query = _context.Problems.AsNoTracking().AsQueryable();

            // Search filter: match title (case-insensitive) or exact problem number
            if (!string.IsNullOrWhiteSpace(parameters.Search)) {
                var searchTerm = parameters.Search.Trim().ToLower();
                if (int.TryParse(searchTerm, out var searchNumber)) {
                    query = query.Where(p => p.ProblemNumber == searchNumber || p.Title.ToLower().Contains(searchTerm));
                } else {
                    query = query.Where(p => p.Title.ToLower().Contains(searchTerm));
                }
            }

            // Category filter: problem must belong to ALL selected categories
            if (parameters.CategoryIds != null && parameters.CategoryIds.Any()) {
                foreach (var categoryId in parameters.CategoryIds) {
                    query = query.Where(p => p.ProblemCategories.Any(pc => pc.CategoryId == categoryId));
                }
            }

            // Get filtered total count (for pagination)
            var totalCount = await query.CountAsync();

            var problems = await query
                .OrderBy(p => p.ProblemNumber)
                .Skip((parameters.PageNumber - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .Include(p => p.ProblemCategories)
                    .ThenInclude(pc => pc.Category)
                .Select(p => new ProblemSummaryModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    ProblemNumber = p.ProblemNumber,
                    Difficulty = p.Difficulty,
                    Categories = p.ProblemCategories.Select(pc => new CategoryModel {
                        Id = pc.Category != null ? pc.Category.Id : Guid.Empty,
                        Name = pc.Category != null ? pc.Category.Name : "Unknown",
                        Slug = pc.Category != null ? pc.Category.Slug : "unknown"
                    }).ToList()
                })
                .ToListAsync();

            return new ProblemSummaryListModel {
                Problems = problems,
                TotalCount = totalCount,
                PageNumber = parameters.PageNumber,
                PageSize = parameters.PageSize
            };
        }

        public async Task<ProblemDetailModel> GetProblemByIdAsync(Guid problemId) {
            _logger.LogInformation("Fetching problem details for Id {ProblemId}", problemId);

            var problem = await _context.Problems
               .AsNoTracking()
               .AsSplitQuery()
               .Include(p => p.Author)
               .Include(p => p.ProblemCategories)
                    .ThenInclude(pc => pc.Category)
               .Include(p => p.TestCases)
               .FirstOrDefaultAsync(p => p.Id == problemId)
               ?? throw new NotFoundException("Problem with ID " + problemId + " not found.");

            var testCases = problem.TestCases
                .OrderBy(testCase => testCase.Id)
                .Select(testCase => new TestCaseModel {
                    Id = testCase.Id,
                    Input = testCase.Input,
                    ExpectedOutput = testCase.ExpectedOutput,
                    InputLabels = DeserializeStringList(testCase.InputLabelsJson),
                    IsHidden = testCase.IsHidden
                })
                .ToList();

            return new ProblemDetailModel {
                Id = problem.Id,
                ProblemNumber = problem.ProblemNumber,
                Title = problem.Title,
                Description = problem.Description,
                Difficulty = problem.Difficulty,
                StarterCode = problem.StarterCode,
                Hints = DeserializeStringList(problem.HintsJson),
                AuthorName = problem.Author != null ? (problem.Author.DisplayName ?? problem.Author.Username) : "Unknown",
                Categories = problem.ProblemCategories.Select(pc => new CategoryModel {
                    Id = pc.Category != null ? pc.Category.Id : Guid.Empty,
                    Name = pc.Category != null ? pc.Category.Name : "Unknown",
                    Slug = pc.Category != null ? pc.Category.Slug : "unknown"
                }).ToList(),
                SampleTestCases = testCases.Where(testCase => !testCase.IsHidden).ToList(),
                TestCases = testCases
            };
        }

        public async Task<ProblemDetailModel> CreateProblemAsync(ProblemCreateModel model, Guid authorId) {
            _logger.LogInformation("Attempting to create a new problem with Title: {Title}", model.Title);

            ///Starting a database transaction that ensures that if *any* part of the creation fails,
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
                    HintsJson = SerializeStringList(model.Hints),
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

                    //Ensuring we update the ProblemCategories list corresponding to the current problem
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
                throw;
            }
        }

        public async Task<ProblemDetailModel> UpdateProblemAsync(Guid problemId, ProblemUpdateModel model) {
            _logger.LogInformation("Attempting to update problem {ProblemId}", problemId);

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try {
                var problem = await _context.Problems
                    .Include(p => p.ProblemCategories)
                    .FirstOrDefaultAsync(p => p.Id == problemId)
                    ?? throw new NotFoundException("Problem not found.");

                problem.Title = model.Title;
                problem.Description = model.Description;
                problem.Difficulty = model.Difficulty;
                problem.StarterCode = model.StarterCode;
                problem.HintsJson = SerializeStringList(model.Hints);

                var nextCategoryIds = model.CategoryIDs ?? new List<Guid>();
                var categories = await _context.Categories
                    .Where(c => nextCategoryIds.Contains(c.Id))
                    .ToListAsync();

                if (categories.Count != nextCategoryIds.Count) {
                    var foundIds = categories.Select(c => c.Id);
                    var notFoundIds = string.Join(", ", nextCategoryIds.Except(foundIds));
                    throw new NotFoundException("One or more categories not found. Invalid IDs: " + notFoundIds);
                }

                _context.ProblemCategories.RemoveRange(problem.ProblemCategories);
                foreach (var category in categories) {
                    problem.ProblemCategories.Add(new ProblemCategory {
                        ProblemId = problem.Id,
                        CategoryId = category.Id
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await GetProblemByIdAsync(problemId);
            } catch {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<CategoryModel>> GetAllCategoriesAsync() {
            return await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CategoryModel {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug
                })
                .ToListAsync();
        }

        public async Task<ProblemDetailModel> AddTestCasesAsync(Guid problemId, List<TestCaseCreateModel> testCases) {
            var problem = await _context.Problems.FindAsync(problemId)
                ?? throw new NotFoundException("Problem not found.");

            foreach (var tc in testCases) {
                _context.TestCases.Add(new TestCase {
                    Id = Guid.NewGuid(),
                    ProblemId = problemId,
                    Input = tc.Input,
                    ExpectedOutput = tc.ExpectedOutput,
                    InputLabelsJson = SerializeStringList(tc.InputLabels),
                    IsHidden = tc.IsHidden
                });
            }

            await _context.SaveChangesAsync();
            return await GetProblemByIdAsync(problemId);
        }

        public async Task<ProblemDetailModel> ReplaceTestCasesAsync(Guid problemId, List<TestCaseCreateModel> testCases) {
            var problem = await _context.Problems
                .Include(p => p.TestCases)
                .FirstOrDefaultAsync(p => p.Id == problemId)
                ?? throw new NotFoundException("Problem not found.");

            _context.TestCases.RemoveRange(problem.TestCases);

            foreach (var tc in testCases) {
                _context.TestCases.Add(new TestCase {
                    Id = Guid.NewGuid(),
                    ProblemId = problemId,
                    Input = tc.Input,
                    ExpectedOutput = tc.ExpectedOutput,
                    InputLabelsJson = SerializeStringList(tc.InputLabels),
                    IsHidden = tc.IsHidden
                });
            }

            await _context.SaveChangesAsync();
            return await GetProblemByIdAsync(problemId);
        }

        private static string? SerializeStringList(IEnumerable<string>? values) {
            var cleaned = values?
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList() ?? new List<string>();

            return cleaned.Count == 0 ? null : JsonSerializer.Serialize(cleaned);
        }

        private static List<string> DeserializeStringList(string? json) {
            if (string.IsNullOrWhiteSpace(json)) {
                return new List<string>();
            }

            try {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            } catch {
                return new List<string>();
            }
        }
    }
}
