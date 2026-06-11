using System.Security.Claims;
using dotnetBitSmith.Helpers;
using Microsoft.AspNetCore.Mvc;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Problems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using dotnetBitSmith.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace dotnetBitSmith.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class ProblemController : ControllerBase {
        private readonly IProblemService _problemService; //We are using IProblemService instead of ProblemService to ensure there's no Dependency Inversion
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProblemController> _logger;

        public ProblemController(IProblemService problemService, ApplicationDbContext context, ILogger<ProblemController> logger) {
            _problemService = problemService;
            _context = context;
            _logger = logger;
        }

        [HttpGet]    
        [ProducesResponseType(typeof(ProblemSummaryListModel), StatusCodes.Status200OK)] //just for documentation in Swagger UI
        // [FromQuery] tells .NET to look for ?PageNumber=1&PageSize=10 in the URL
        public async Task<ActionResult<IEnumerable<ProblemSummaryModel>>> GetProblemsAsync([FromQuery] ProblemParametersModel parameters) {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdString) && Guid.TryParse(userIdString, out var userId)) {
                parameters.UserId = userId;
            }
            var problems = await _problemService.GetProblemsAsync(parameters);
            return Ok(problems);
        }

        [HttpGet("{problemId}", Name = "GetProblemById")]
        [ProducesResponseType(typeof(IEnumerable<ProblemDetailModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProblemDetailModel>> GetProblemByIdAsync(Guid problemId) {
            Guid? userId = null;
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdString) && Guid.TryParse(userIdString, out var parsedId)) {
                userId = parsedId;
            }
            var problem = await _problemService.GetProblemByIdAsync(problemId, userId);
            if (problem == null) return NotFound();

            // Hide test cases from regular users
            if (!User.IsInRole("Admin"))
            {
                problem.TestCases = null;
            }

            return Ok(problem);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ProblemDetailModel), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ProblemDetailModel>> CreateProblem([FromBody] ProblemCreateModel model) {
            var authorId = User.GetUserId();
            var newProblem = await _problemService.CreateProblemAsync(model, authorId);
            return CreatedAtRoute("GetProblemById", new { problemId = newProblem.Id }, newProblem);
        }

        [HttpPut("{problemId}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ProblemDetailModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ProblemDetailModel>> UpdateProblem(Guid problemId, [FromBody] ProblemUpdateModel model) {
            var updatedProblem = await _problemService.UpdateProblemAsync(problemId, model);
            return Ok(updatedProblem);
        }

        [HttpGet("categories")]
        [ProducesResponseType(typeof(IEnumerable<CategoryModel>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<CategoryModel>>> GetCategories() {
            var categories = await _problemService.GetAllCategoriesAsync();
            return Ok(categories);
        }

        [HttpPost("{problemId}/testcases")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ProblemDetailModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProblemDetailModel>> AddTestCases(Guid problemId, [FromBody] List<TestCaseCreateModel> testCases) {
            var result = await _problemService.AddTestCasesAsync(problemId, testCases);
            return Ok(result);
        }

        [HttpPut("{problemId}/testcases")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ProblemDetailModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProblemDetailModel>> ReplaceTestCases(Guid problemId, [FromBody] List<TestCaseCreateModel> testCases) {
            var result = await _problemService.ReplaceTestCasesAsync(problemId, testCases);
            return Ok(result);
        }

        [HttpGet("pod")]
        [ProducesResponseType(typeof(ProblemSummaryModel), StatusCodes.Status200OK)]
        public async Task<ActionResult<ProblemSummaryModel>> GetProblemOfTheDay([FromQuery] string? dateStr) {
            DateOnly date = DateOnly.FromDateTime(DateTime.UtcNow);
            if (!string.IsNullOrEmpty(dateStr) && DateOnly.TryParse(dateStr, out var parsed)) {
                date = parsed;
            }
            var pod = await _problemService.GetProblemOfTheDayAsync(date);
            return Ok(pod);
        }

        [HttpPost("pod")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ProblemSummaryModel), StatusCodes.Status200OK)]
        public async Task<ActionResult<ProblemSummaryModel>> SetProblemOfTheDay([FromQuery] string dateStr, [FromQuery] Guid problemId) {
            if (!DateOnly.TryParse(dateStr, out var date)) return BadRequest("Invalid date format.");
            var pod = await _problemService.SetProblemOfTheDayAsync(date, problemId);
            return Ok(pod);
        }

        [HttpGet("pod/activity")]
        [Authorize]
        [ProducesResponseType(typeof(PoDActivityModel), StatusCodes.Status200OK)]
        public async Task<ActionResult<PoDActivityModel>> GetPoDActivity([FromQuery] string? dateStr, [FromQuery] int? tzOffset) {
            var userId = User.GetUserId();
            DateOnly todayLocal = DateOnly.FromDateTime(DateTime.UtcNow);
            if (!string.IsNullOrEmpty(dateStr) && DateOnly.TryParse(dateStr, out var parsed)) {
                todayLocal = parsed;
            }
            int offset = tzOffset ?? 0;

            var activity = await _problemService.GetPoDActivityAsync(userId, offset, todayLocal);
            return Ok(activity);
        }

        /// <summary>
        /// Upload test cases from a CSV or JSON file.
        /// CSV format (header required): isHidden,input,expectedOutput,inputLabels
        /// JSON format: [{isHidden:bool, input:string, expectedOutput:string, inputLabels:string[]}]
        /// This keeps sensitive expected outputs off the frontend entirely.
        /// </summary>
        [HttpPost("{problemId}/testcases/upload")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ProblemDetailModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProblemDetailModel>> UploadTestCases(Guid problemId, IFormFile file) {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".csv" && extension != ".json")
                return BadRequest("Only .csv or .json files are accepted.");

            if (file.Length > 2 * 1024 * 1024) // 2 MB max
                return BadRequest("File size must not exceed 2 MB.");

            List<TestCaseCreateModel> testCases;

            try {
                using var stream = file.OpenReadStream();
                using var reader = new StreamReader(stream);

                if (extension == ".json") {
                    var content = await reader.ReadToEndAsync();
                    testCases = System.Text.Json.JsonSerializer.Deserialize<List<TestCaseCreateModel>>(content,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? throw new FormatException("Invalid JSON structure.");
                } else {
                    // CSV: skip header row, parse each subsequent line
                    testCases = new List<TestCaseCreateModel>();
                    string? line;
                    bool isFirstLine = true;
                    while ((line = await reader.ReadLineAsync()) != null) {
                        if (isFirstLine) { isFirstLine = false; continue; } // skip header
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Support quoted CSV fields (for multi-line inputs)
                        var parts = ParseCsvLine(line);
                        if (parts.Length < 3)
                            return BadRequest($"Malformed CSV row: {line.Substring(0, Math.Min(50, line.Length))}");

                        bool isHidden = false;
                        if (parts.Length > 0)
                        {
                            var rawHidden = parts[0].Trim().ToLower();
                            isHidden = rawHidden == "true" || rawHidden == "1";
                        }

                        testCases.Add(new TestCaseCreateModel {
                            IsHidden = isHidden,
                            Input = parts[1].Trim().Replace("\\n", "\n"),
                            ExpectedOutput = parts[2].Trim().Replace("\\n", "\n"),
                            InputLabels = parts.Length > 3
                                ? parts[3].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                                : new List<string>()
                        });
                    }
                }
            } catch (Exception ex) {
                return BadRequest($"Failed to parse file: {ex.Message}");
            }

            if (testCases.Count == 0)
                return BadRequest("File contained no valid test cases.");

            var result = await _problemService.AddTestCasesAsync(problemId, testCases);
            return Ok(result);
        }

        private static string[] ParseCsvLine(string line) {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();
            foreach (char c in line) {
                if (c == '"') { inQuotes = !inQuotes; }
                else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
                else { current.Append(c); }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }


        [HttpGet("debug-database")]
        public async Task<IActionResult> DebugDatabase([FromServices] dotnetBitSmith.Data.ApplicationDbContext context) {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "problems.json");
            var exists = System.IO.File.Exists(filePath);
            var size = exists ? new System.IO.FileInfo(filePath).Length : 0;
            
            string jsonProblemsSnippet = "";
            if (exists) {
                try {
                    var jsonContent = await System.IO.File.ReadAllTextAsync(filePath);
                    using (var doc = JsonDocument.Parse(jsonContent)) {
                        var root = doc.RootElement;
                        var problems = root.GetProperty("problems");
                        if (problems.ValueKind == JsonValueKind.Array && problems.GetArrayLength() > 0) {
                            var firstProblem = problems[0];
                            var testCases = firstProblem.GetProperty("testCases");
                            jsonProblemsSnippet = $"First problem title: {firstProblem.GetProperty("title").GetString()}, TestCases count: {testCases.GetArrayLength()}";
                        }
                    }
                } catch (Exception ex) {
                    jsonProblemsSnippet = "Error: " + ex.Message;
                }
            }

            var problemCount = await context.Problems.CountAsync();
            var testCaseCount = await context.TestCases.CountAsync();

            return Ok(new {
                problemCount,
                testCaseCount,
                filePath,
                exists,
                size,
                snippet = jsonProblemsSnippet
            });
        }

        [HttpPost("generate-test-cases")]
        public async Task<IActionResult> GenerateTestCases(
            [FromServices] dotnetBitSmith.Data.ApplicationDbContext context,
            [FromServices] ICompilationService compilationService,
            [FromServices] IHttpClientFactory httpClientFactory,
            [FromQuery] int limit = 5) 
        {
            try {
                var client = httpClientFactory.CreateClient();
                var result = await TestCaseGenerator.GenerateTestCasesForPendingProblemsAsync(context, compilationService, client, limit);
                return Ok(new { message = result });
            } catch (Exception ex) {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("make-all-test-cases-visible")]
        public async Task<IActionResult> MakeAllTestCasesVisible() {
            try {
                int updatedCount = await _context.Database.ExecuteSqlRawAsync("UPDATE TestCases SET IsHidden = 0;");
                return Ok(new { message = $"Successfully updated {updatedCount} test cases to be visible.", count = updatedCount });
            } catch (Exception ex) {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("import")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ImportResultModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ImportResultModel>> ImportProblems(
            IFormFile file,
            [FromQuery] bool clearExisting = false,
            [FromQuery] int easyLimit = -1,
            [FromQuery] int mediumLimit = -1,
            [FromQuery] int hardLimit = -1) 
        {
            if (file == null || file.Length == 0) {
                return BadRequest("No file uploaded or file is empty.");
            }

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) {
                return BadRequest("Only JSON files are allowed.");
            }

            try {
                string jsonContent;
                using (var reader = new System.IO.StreamReader(file.OpenReadStream())) {
                    jsonContent = await reader.ReadToEndAsync();
                }

                var authorId = User.GetUserId();
                var result = await ProblemSeeder.SeedProblemsFromJsonAsync(
                    jsonContent, 
                    _context, 
                    _logger, 
                    authorId, 
                    clearExisting, 
                    easyLimit, 
                    mediumLimit, 
                    hardLimit);
                
                return Ok(result);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error occurred during problem file import.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred during import: {ex.Message}");
            }
        }

        [HttpPost("import-local")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ImportResultModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ImportResultModel>> ImportLocalProblems(
            [FromQuery] string fileName = "problems.json",
            [FromQuery] bool clearExisting = false,
            [FromQuery] int easyLimit = -1,
            [FromQuery] int mediumLimit = -1,
            [FromQuery] int hardLimit = -1) 
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            if (!System.IO.File.Exists(filePath)) {
                return BadRequest($"File not found at: {filePath}");
            }

            try {
                _logger.LogInformation("Reading local problems JSON file from: {Path}", filePath);
                var jsonContent = await System.IO.File.ReadAllTextAsync(filePath);
                
                var authorId = User.GetUserId();
                var result = await ProblemSeeder.SeedProblemsFromJsonAsync(
                    jsonContent, 
                    _context, 
                    _logger, 
                    authorId, 
                    clearExisting, 
                    easyLimit, 
                    mediumLimit, 
                    hardLimit);
                
                return Ok(result);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error occurred during local file import.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }
    }
}
