using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Entities.Enums;
using dotnetBitSmith.Models.Problems;

namespace dotnetBitSmith.Helpers {
    public static class ProblemSeeder {
        
        // Deserialization models
        private class LeetCodeImportJson {
            public List<ProblemJson> Problems { get; set; }
        }

        private class ProblemJson {
            public int ProblemNumber { get; set; }
            public string Title { get; set; }
            public string Slug { get; set; }
            public string Difficulty { get; set; }
            public string Content { get; set; }
            public List<string> TestCases { get; set; }
            public string SampleTestCase { get; set; }
            public JsonElement MetaData { get; set; }
            public List<string> Hints { get; set; }
            public JsonElement TopicTags { get; set; }
            public List<CodeSnippetJson> CodeSnippets { get; set; }
            public string LeetcodeUrl { get; set; }
        }

        private class CodeSnippetJson {
            public string Lang { get; set; }
            public string LangSlug { get; set; }
            public string Code { get; set; }
        }

        public static async Task<ImportResultModel> SeedProblemsFromJsonAsync(
            string jsonContent, 
            ApplicationDbContext context, 
            ILogger logger,
            Guid? authorId = null,
            bool clearExisting = false,
            int easyLimit = -1,
            int mediumLimit = -1,
            int hardLimit = -1) 
        {
            var result = new ImportResultModel();
            
            if (string.IsNullOrWhiteSpace(jsonContent)) {
                result.Errors++;
                result.ErrorMessages.Add("JSON content is empty.");
                return result;
            }

            LeetCodeImportJson importData;
            try {
                var options = new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true
                };
                importData = JsonSerializer.Deserialize<LeetCodeImportJson>(jsonContent, options);
            } catch (Exception ex) {
                logger.LogError(ex, "Failed to deserialize problems JSON.");
                result.Errors++;
                result.ErrorMessages.Add($"JSON Deserialization Error: {ex.Message}");
                return result;
            }

            if (importData?.Problems == null || !importData.Problems.Any()) {
                result.ErrorMessages.Add("No problems found in JSON.");
                return result;
            }

            // 1. Clear existing data if requested
            if (clearExisting) {
                logger.LogInformation("Clearing existing problems and related community data from the database.");
                try {
                    using (var transaction = await context.Database.BeginTransactionAsync()) {
                        context.Comments.RemoveRange(context.Comments);
                        context.Votes.RemoveRange(context.Votes);
                        context.Submissions.RemoveRange(context.Submissions);
                        context.Solutions.RemoveRange(context.Solutions);
                        context.TestCases.RemoveRange(context.TestCases);
                        context.ProblemCategories.RemoveRange(context.ProblemCategories);
                        context.Problems.RemoveRange(context.Problems);
                        await context.SaveChangesAsync();
                        
                        // Truncate tables and reset sequences cleanly using Postgres cascade
                        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Comments\", \"Votes\", \"Submissions\", \"Solutions\", \"TestCases\", \"ProblemCategories\", \"Problems\" RESTART IDENTITY CASCADE;");
                        
                        await transaction.CommitAsync();
                    }
                    logger.LogInformation("Database cleared and identity reset successfully.");
                } catch (Exception ex) {
                    logger.LogError(ex, "Failed to clear database before seeding.");
                    result.Errors++;
                    result.ErrorMessages.Add($"Database cleanup error: {ex.Message}");
                    return result;
                }
            }

            result.TotalFound = importData.Problems.Count;
            logger.LogInformation("Found {Count} problems in JSON. Starting database ingestion.", result.TotalFound);

            int easyImported = 0;
            int mediumImported = 0;
            int hardImported = 0;

            int currentSeqNumber = 1;
            if (!clearExisting) {
                var maxNumber = await context.Problems.MaxAsync(p => (int?)p.ProblemNumber) ?? 0;
                currentSeqNumber = maxNumber + 1;
            }

            var sortedProblems = importData.Problems.OrderBy(p => p.ProblemNumber).ToList();

            foreach (var problemJson in sortedProblems) {
                // Check if we hit all limits to stop early
                bool easyDone = (easyLimit < 0 || easyImported >= easyLimit);
                bool mediumDone = (mediumLimit < 0 || mediumImported >= mediumLimit);
                bool hardDone = (hardLimit < 0 || hardImported >= hardLimit);
                if (easyDone && mediumDone && hardDone) {
                    logger.LogInformation("All requested limits reached (Easy: {Easy}, Medium: {Medium}, Hard: {Hard}). Stopping import early.", easyImported, mediumImported, hardImported);
                    break;
                }

                try {
                    // Parse difficulty
                    if (!Enum.TryParse<ProblemDifficulty>(problemJson.Difficulty, true, out var difficulty)) {
                        difficulty = ProblemDifficulty.Medium; // default fallback
                    }

                    // Apply difficulty limits
                    if (difficulty == ProblemDifficulty.Easy && easyLimit >= 0 && easyImported >= easyLimit) {
                        continue;
                    }
                    if (difficulty == ProblemDifficulty.Medium && mediumLimit >= 0 && mediumImported >= mediumLimit) {
                        continue;
                    }
                    if (difficulty == ProblemDifficulty.Hard && hardLimit >= 0 && hardImported >= hardLimit) {
                        continue;
                    }

                    // Check for duplicates (only if we didn't clear the DB)
                    if (!clearExisting) {
                        var exists = await context.Problems.AnyAsync(p => p.Title == problemJson.Title);
                        if (exists) {
                            result.SkippedDuplicates++;
                            logger.LogInformation("Skipping duplicate problem: {Title}", problemJson.Title);
                            continue;
                        }
                    }

                    // Extract Multi-Language Starter Code
                    string starterCode = null;
                    if (problemJson.CodeSnippets != null) {
                        var codes = new Dictionary<string, string>();
                        foreach (var snippet in problemJson.CodeSnippets) {
                            var langKey = snippet.LangSlug?.ToLower();
                            if (langKey == "python3") langKey = "python";
                            
                            // Keep only the supported languages
                            if (langKey == "csharp" || langKey == "python" || langKey == "java" || langKey == "cpp" || langKey == "c" || langKey == "javascript") {
                                codes[langKey] = snippet.Code;
                            }
                        }
                        starterCode = JsonSerializer.Serialize(codes);
                    }

                    // Create Problem Entity
                    var problem = new Problem {
                        Id = Guid.NewGuid(),
                        ProblemNumber = currentSeqNumber, // Assign sequential number
                        Title = problemJson.Title,
                        Description = problemJson.Content,
                        Difficulty = difficulty,
                        StarterCode = starterCode,
                        HintsJson = SerializeStringList(problemJson.Hints),
                        CreatedAt = DateTime.UtcNow,
                        AuthorId = authorId
                    };

                    // Parse and map Topic Tags (Categories)
                    var tags = new List<string>();
                    if (problemJson.TopicTags.ValueKind == JsonValueKind.Array) {
                        foreach (var tagElement in problemJson.TopicTags.EnumerateArray()) {
                            if (tagElement.ValueKind == JsonValueKind.String) {
                                tags.Add(tagElement.GetString());
                            } else if (tagElement.ValueKind == JsonValueKind.Object && tagElement.TryGetProperty("name", out var nameProp)) {
                                tags.Add(nameProp.GetString());
                            }
                        }
                    }

                    foreach (var tagName in tags) {
                        if (string.IsNullOrWhiteSpace(tagName)) continue;
                        
                        var categorySlug = tagName.ToLower().Replace(" ", "-");
                        var category = await context.Categories.FirstOrDefaultAsync(c => c.Slug == categorySlug);
                        if (category == null) {
                            category = new Category {
                                Id = Guid.NewGuid(),
                                Name = tagName,
                                Slug = categorySlug
                            };
                            context.Categories.Add(category);
                            await context.SaveChangesAsync();
                        }

                        problem.ProblemCategories.Add(new ProblemCategory {
                            ProblemId = problem.Id,
                            CategoryId = category.Id
                        });
                    }

                    // Extract Expected Outputs from description HTML
                    var expectedOutputs = ExtractExpectedOutputs(problemJson.Content);

                    // Parse Parameters & group inputs
                    int paramCount = 1;
                    var inputLabels = new List<string>();
                    if (problemJson.MetaData.ValueKind == JsonValueKind.Object && 
                        problemJson.MetaData.TryGetProperty("params", out var paramsProp) && 
                        paramsProp.ValueKind == JsonValueKind.Array) 
                    {
                        paramCount = paramsProp.GetArrayLength();
                        inputLabels = ExtractParameterNames(paramsProp);
                    }
                    if (paramCount <= 0) paramCount = 1;

                    var inputs = new List<string>();
                    if (problemJson.TestCases != null) {
                        for (int i = 0; i < problemJson.TestCases.Count; i += paramCount) {
                            var chunk = new List<string>();
                            for (int j = 0; j < paramCount && (i + j) < problemJson.TestCases.Count; j++) {
                                chunk.Add(problemJson.TestCases[i + j]);
                            }
                            inputs.Add(string.Join("\n", chunk));
                        }
                    }

                    // Add TestCases to Problem
                    for (int i = 0; i < inputs.Count; i++) {
                        var input = inputs[i];
                        var expectedOutput = (i < expectedOutputs.Count) ? expectedOutputs[i] : "";

                        problem.TestCases.Add(new TestCase {
                            Id = Guid.NewGuid(),
                            Input = input,
                            ExpectedOutput = expectedOutput,
                            InputLabelsJson = SerializeStringList(inputLabels),
                            IsHidden = false,
                            ProblemId = problem.Id
                        });
                    }

                    // Save problem using raw SQL
                    using (var transaction = await context.Database.BeginTransactionAsync()) {
                        try {
                             var pId = new NpgsqlParameter("@Id", problem.Id);
                             var pProblemNumber = new NpgsqlParameter("@ProblemNumber", problem.ProblemNumber);
                             var pTitle = new NpgsqlParameter("@Title", problem.Title ?? (object)DBNull.Value);
                             var pDescription = new NpgsqlParameter("@Description", problem.Description ?? (object)DBNull.Value);
                             var pStarterCode = new NpgsqlParameter("@StarterCode", problem.StarterCode ?? (object)DBNull.Value);
                             var pHintsJson = new NpgsqlParameter("@HintsJson", problem.HintsJson ?? (object)DBNull.Value);
                             var pDifficulty = new NpgsqlParameter("@Difficulty", problem.Difficulty.ToString());
                             var pCreatedAt = new NpgsqlParameter("@CreatedAt", problem.CreatedAt);
                             var pAuthorId = new NpgsqlParameter("@AuthorId", NpgsqlTypes.NpgsqlDbType.Uuid) {
                                 Value = (object?)problem.AuthorId ?? DBNull.Value
                             };

                             var sql = @"
                                 INSERT INTO ""Problems"" (""Id"", ""ProblemNumber"", ""Title"", ""Description"", ""StarterCode"", ""HintsJson"", ""Difficulty"", ""CreatedAt"", ""AuthorId"")
                                 VALUES (@Id, @ProblemNumber, @Title, @Description, @StarterCode, @HintsJson, @Difficulty, @CreatedAt, @AuthorId);
                             ";
                             await context.Database.ExecuteSqlRawAsync(sql, new object[] {
                                 pId, pProblemNumber, pTitle, pDescription, pStarterCode, pHintsJson, pDifficulty, pCreatedAt, pAuthorId
                             });

                            // Now save EF tracked related tables (Categories and TestCases)
                            if (problem.ProblemCategories.Any()) {
                                await context.ProblemCategories.AddRangeAsync(problem.ProblemCategories);
                            }
                            if (problem.TestCases.Any()) {
                                await context.TestCases.AddRangeAsync(problem.TestCases);
                            }

                            await context.SaveChangesAsync();
                            await transaction.CommitAsync();
                            
                            result.SuccessfullyImported++;
                            currentSeqNumber++; // Increment sequence number
                            if (difficulty == ProblemDifficulty.Easy) easyImported++;
                            else if (difficulty == ProblemDifficulty.Medium) mediumImported++;
                            else if (difficulty == ProblemDifficulty.Hard) hardImported++;

                            logger.LogInformation("Successfully imported problem: #{Number} - {Title} ({Diff})", problem.ProblemNumber, problem.Title, difficulty);
                        } catch (Exception ex) {
                            await transaction.RollbackAsync();
                            throw new Exception($"Transaction save failed: {ex.Message}", ex);
                        }
                    }

                } catch (Exception ex) {
                    logger.LogError(ex, "Failed to import problem #{Number} - {Title}", problemJson.ProblemNumber, problemJson.Title);
                    result.Errors++;
                    result.ErrorMessages.Add($"Problem #{problemJson.ProblemNumber} Error: {ex.Message}");
                }
            }

            // Sync/Reset the auto-increment identity sequence for problems
            try {
                await context.Database.ExecuteSqlRawAsync(@"
                    SELECT setval(pg_get_serial_sequence('""Problems""', 'ProblemNumber'), COALESCE(MAX(""ProblemNumber""), 0) + 1, false) FROM ""Problems"";
                ");
            } catch (Exception ex) {
                logger.LogWarning(ex, "Failed to sync ProblemNumber sequence after import.");
            }

            logger.LogInformation("Import summary - Easy: {Easy}, Medium: {Medium}, Hard: {Hard}", easyImported, mediumImported, hardImported);
            return result;
        }

        private static List<string> ExtractExpectedOutputs(string htmlContent) {
            var outputs = new List<string>();
            if (string.IsNullOrWhiteSpace(htmlContent)) return outputs;

            // Look for "Output:" followed by the output inside span (example-io), pre or code tags
            var matches = Regex.Matches(htmlContent, 
                @"Output:<\/strong>\s*(?:<span\s+class=""example-io"">|<pre>|<code>)?(.*?)(?:<\/span>|<\/pre>|<\/code>|(?=<\/p>)|(?=<p>)|(?=\n)|(?=<div))", 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in matches) {
                var val = match.Groups[1].Value;
                // Remove any internal html tags (e.g. <code>)
                val = Regex.Replace(val, "<.*?>", "").Trim();
                val = WebUtility.HtmlDecode(val);

                // Strip leading/trailing double quotes if it was parsed as string representation
                if (val.StartsWith("\"") && val.EndsWith("\"") && val.Length >= 2) {
                    val = val.Substring(1, val.Length - 2);
                }

                outputs.Add(val);
            }

            return outputs;
        }

        private static List<string> ExtractParameterNames(JsonElement paramsProp) {
            var names = new List<string>();

            foreach (var param in paramsProp.EnumerateArray()) {
                if (param.ValueKind == JsonValueKind.Object && param.TryGetProperty("name", out var nameProp)) {
                    var name = nameProp.GetString();
                    if (!string.IsNullOrWhiteSpace(name)) {
                        names.Add(name);
                    }
                }
            }

            return names;
        }

        private static string? SerializeStringList(IEnumerable<string>? values) {
            var cleaned = values?
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList() ?? new List<string>();

            return cleaned.Count == 0 ? null : JsonSerializer.Serialize(cleaned);
        }
    }
}
