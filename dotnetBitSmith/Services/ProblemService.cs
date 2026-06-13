using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Exceptions;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Problems;
using dotnetBitSmith.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace dotnetBitSmith.Services {
    public class ProblemService : IProblemService {

        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProblemService> _logger;
        private readonly IMemoryCache _cache;

        public ProblemService(ApplicationDbContext context, ILogger<ProblemService> logger, IMemoryCache cache) {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        public static string GenerateSlug(string title) {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;
            var slug = title.ToLowerInvariant();
            var sb = new System.Text.StringBuilder();
            bool prevWasHyphen = false;
            foreach (char c in slug) {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) {
                    sb.Append(c);
                    prevWasHyphen = false;
                } else if (c == ' ' || c == '-' || c == '_') {
                    if (!prevWasHyphen && sb.Length > 0) {
                        sb.Append('-');
                        prevWasHyphen = true;
                    }
                }
            }
            return sb.ToString().Trim('-');
        }

        public async Task<ProblemDetailModel> GetProblemBySlugAsync(string slug, Guid? userId = null) {
            _logger.LogInformation("Fetching problem details for slug {Slug}", slug);
            var problemLookup = await _context.Problems
                .AsNoTracking()
                .Select(p => new { p.Id, p.Title })
                .ToListAsync();
            var matchedProblem = problemLookup
                .FirstOrDefault(p => GenerateSlug(p.Title).Equals(slug, System.StringComparison.OrdinalIgnoreCase));
            if (matchedProblem == null) {
                throw new NotFoundException("Problem with slug " + slug + " not found.");
            }
            return await GetProblemByIdAsync(matchedProblem.Id, userId);
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

            // Status filter: requires UserId to be set
            if (parameters.UserId.HasValue && !string.IsNullOrEmpty(parameters.StatusFilter)) {
                var statusFilter = parameters.StatusFilter.ToLower();
                if (statusFilter == "solved") {
                    query = query.Where(p => _context.Submissions.Any(s => s.UserId == parameters.UserId.Value && s.ProblemId == p.Id && s.Status == SubmissionStatus.Accepted));
                } else if (statusFilter == "attempted") {
                    query = query.Where(p => _context.Submissions.Any(s => s.UserId == parameters.UserId.Value && s.ProblemId == p.Id) &&
                                            !_context.Submissions.Any(s => s.UserId == parameters.UserId.Value && s.ProblemId == p.Id && s.Status == SubmissionStatus.Accepted));
                } else if (statusFilter == "unattempted") {
                    query = query.Where(p => !_context.Submissions.Any(s => s.UserId == parameters.UserId.Value && s.ProblemId == p.Id));
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

            foreach (var p in problems) {
                p.Slug = GenerateSlug(p.Title);
            }

            if (parameters.UserId.HasValue) {
                var problemIds = problems.Select(p => p.Id).ToList();
                var userSubs = await _context.Submissions
                    .Where(s => s.UserId == parameters.UserId.Value && problemIds.Contains(s.ProblemId))
                    .Select(s => new { s.ProblemId, s.Status })
                    .ToListAsync();
                
                foreach (var p in problems) {
                    var probSubs = userSubs.Where(s => s.ProblemId == p.Id).ToList();
                    if (probSubs.Any(s => s.Status == SubmissionStatus.Accepted)) {
                        p.Status = "Solved";
                    } else if (probSubs.Any()) {
                        p.Status = "Attempted";
                    } else {
                        p.Status = "Unattempted";
                    }
                }
            }

            var totalEasy = await query.CountAsync(p => p.Difficulty == ProblemDifficulty.Easy);
            var totalMedium = await query.CountAsync(p => p.Difficulty == ProblemDifficulty.Medium);
            var totalHard = await query.CountAsync(p => p.Difficulty == ProblemDifficulty.Hard);

            return new ProblemSummaryListModel {
                Problems = problems,
                TotalCount = totalCount,
                TotalEasy = totalEasy,
                TotalMedium = totalMedium,
                TotalHard = totalHard,
                PageNumber = parameters.PageNumber,
                PageSize = parameters.PageSize
            };
        }

        public async Task<ProblemDetailModel> GetProblemByIdAsync(Guid problemId, Guid? userId = null) {
            _logger.LogInformation("Fetching problem details for Id {ProblemId}", problemId);

            var cacheKey = $"problem_detail_{problemId}";
            var detailModel = await _cache.GetOrCreateAsync(cacheKey, async entry => {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                _logger.LogInformation("Cache miss for {CacheKey}. Fetching from database.", cacheKey);

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
                    Slug = GenerateSlug(problem.Title),
                    Description = problem.Description,
                    Difficulty = problem.Difficulty,
                    StarterCode = problem.StarterCode,
                    MetaDataJson = problem.MetaDataJson,
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
            });

            // Clone to avoid modifying the cached reference directly since we enrich it with user status
            var responseModel = new ProblemDetailModel {
                Id = detailModel!.Id,
                ProblemNumber = detailModel.ProblemNumber,
                Title = detailModel.Title,
                Slug = detailModel.Slug,
                Description = detailModel.Description,
                Difficulty = detailModel.Difficulty,
                StarterCode = detailModel.StarterCode,
                MetaDataJson = detailModel.MetaDataJson,
                Hints = detailModel.Hints,
                AuthorName = detailModel.AuthorName,
                Categories = detailModel.Categories,
                SampleTestCases = detailModel.SampleTestCases,
                TestCases = detailModel.TestCases,
                Status = "Unattempted"
            };

            if (userId.HasValue) {
                var userSubs = await _context.Submissions
                    .Where(s => s.UserId == userId.Value && s.ProblemId == problemId)
                    .Select(s => s.Status)
                    .ToListAsync();
                if (userSubs.Any(s => s == SubmissionStatus.Accepted)) {
                    responseModel.Status = "Solved";
                } else if (userSubs.Any()) {
                    responseModel.Status = "Attempted";
                }
            }

            return responseModel;
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
                    MetaDataJson = model.MetaDataJson,
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
                problem.MetaDataJson = model.MetaDataJson;
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

                _cache.Remove($"problem_detail_{problemId}");

                return await GetProblemByIdAsync(problemId);
            } catch {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<CategoryModel>> GetAllCategoriesAsync() {
            return (await _cache.GetOrCreateAsync("categories_all", async entry => {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                _logger.LogInformation("Cache miss for categories_all. Fetching from database.");
                return await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new CategoryModel {
                        Id = c.Id,
                        Name = c.Name,
                        Slug = c.Slug
                    })
                    .ToListAsync();
            })) ?? System.Linq.Enumerable.Empty<CategoryModel>();
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
            _cache.Remove($"problem_detail_{problemId}");
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
            _cache.Remove($"problem_detail_{problemId}");
            return await GetProblemByIdAsync(problemId);
        }

        public async Task<ProblemSummaryModel> GetProblemOfTheDayAsync(DateOnly date) {
            var cacheKey = $"pod_{date:yyyyMMdd}";
            return (await _cache.GetOrCreateAsync(cacheKey, async entry => {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4);
                _logger.LogInformation("Cache miss for POD on {Date}", date);

                var pod = await _context.ProblemOfTheDays
                    .FirstOrDefaultAsync(x => x.Date == date);

                if (pod == null) {
                    // Pick a random problem
                    var count = await _context.Problems.CountAsync();
                    if (count == 0) throw new NotFoundException("No problems available to set as Problem of the Day.");
                    
                    var random = new Random(date.DayNumber);
                    var skip = random.Next(0, count);
                    var randomProblem = await _context.Problems
                        .OrderBy(p => p.ProblemNumber)
                        .Skip(skip)
                        .FirstOrDefaultAsync();
                    
                    pod = new ProblemOfTheDay {
                        Id = Guid.NewGuid(),
                        Date = date,
                        ProblemId = randomProblem!.Id
                    };
                    _context.ProblemOfTheDays.Add(pod);
                    await _context.SaveChangesAsync();
                }

                var problem = await _context.Problems
                    .AsNoTracking()
                    .Include(p => p.ProblemCategories)
                        .ThenInclude(pc => pc.Category)
                    .FirstOrDefaultAsync(p => p.Id == pod.ProblemId)
                    ?? throw new NotFoundException("Problem associated with POD not found.");

                return new ProblemSummaryModel {
                    Id = problem.Id,
                    Title = problem.Title,
                    Slug = GenerateSlug(problem.Title),
                    ProblemNumber = problem.ProblemNumber,
                    Difficulty = problem.Difficulty,
                    Categories = problem.ProblemCategories.Select(pc => new CategoryModel {
                        Id = pc.Category != null ? pc.Category.Id : Guid.Empty,
                        Name = pc.Category != null ? pc.Category.Name : "Unknown",
                        Slug = pc.Category != null ? pc.Category.Slug : "unknown"
                    }).ToList()
                };
            }))!;
        }

        public async Task<ProblemSummaryModel> SetProblemOfTheDayAsync(DateOnly date, Guid problemId) {
            var problem = await _context.Problems.FindAsync(problemId)
                ?? throw new NotFoundException("Problem not found.");

            var pod = await _context.ProblemOfTheDays.FirstOrDefaultAsync(x => x.Date == date);
            if (pod != null) {
                pod.ProblemId = problemId;
            } else {
                pod = new ProblemOfTheDay {
                    Id = Guid.NewGuid(),
                    Date = date,
                    ProblemId = problemId
                };
                _context.ProblemOfTheDays.Add(pod);
            }
            await _context.SaveChangesAsync();

            _cache.Remove($"pod_{date:yyyyMMdd}");

            return await GetProblemOfTheDayAsync(date);
        }

        public async Task<PoDActivityModel> GetPoDActivityAsync(Guid userId, int tzOffsetMinutes, DateOnly todayLocal) {
            // Find all PoDs
            var pods = await _context.ProblemOfTheDays.ToListAsync();
            
            // For each PoD, check if there's a successful submission by the user ON THAT DATE
            var solvedDates = new List<DateOnly>();
            foreach (var pod in pods) {
                // podDateStart in UTC
                var podDateStart = pod.Date.ToDateTime(TimeOnly.MinValue).AddMinutes(tzOffsetMinutes);
                var podDateEnd = podDateStart.AddDays(1);

                var hasSolved = await _context.Submissions.AnyAsync(s => 
                    s.UserId == userId && 
                    s.ProblemId == pod.ProblemId && 
                    s.Status == dotnetBitSmith.Entities.Enums.SubmissionStatus.Accepted &&
                    s.CreatedAt >= podDateStart && 
                    s.CreatedAt < podDateEnd);

                if (hasSolved) {
                    solvedDates.Add(pod.Date);
                }
            }

            var sortedSolved = solvedDates.OrderByDescending(d => d).ToList();
            
            int streak = 0;
            var today = todayLocal;
            var yesterday = today.AddDays(-1);

            DateOnly currentDateToCheck = today;

            if (sortedSolved.Contains(today)) {
                streak++;
                currentDateToCheck = yesterday;
            } else if (sortedSolved.Contains(yesterday)) {
                // If they haven't solved today, but solved yesterday, the streak is still alive
                currentDateToCheck = yesterday;
            } else {
                return new PoDActivityModel { SolvedDates = sortedSolved.Select(d => d.ToString("yyyy-MM-dd")).ToList(), CurrentStreak = 0 };
            }

            // Count backwards from currentDateToCheck
            while (sortedSolved.Contains(currentDateToCheck)) {
                if (currentDateToCheck != today) {
                    streak++;
                }
                currentDateToCheck = currentDateToCheck.AddDays(-1);
            }

            return new PoDActivityModel {
                SolvedDates = sortedSolved.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
                CurrentStreak = streak
            };
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
