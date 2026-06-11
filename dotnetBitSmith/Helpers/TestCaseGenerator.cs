using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Interfaces;

namespace dotnetBitSmith.Helpers {
    public static class TestCaseGenerator {
        
        public static async Task<string> GenerateTestCasesForPendingProblemsAsync(
            ApplicationDbContext context, 
            ICompilationService compilationService, 
            HttpClient httpClient,
            int maxProblemsToProcess = 5) 
        {
            var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "problems.json");
            if (!File.Exists(jsonPath)) {
                return "Error: problems.json not found.";
            }

            // 1. Parse problems.json metadata
            var jsonString = await File.ReadAllTextAsync(jsonPath);
            Dictionary<string, JsonElement> jsonLookup;
            try {
                using (var doc = JsonDocument.Parse(jsonString)) {
                    var problemsArray = doc.RootElement.GetProperty("problems");
                    jsonLookup = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in problemsArray.EnumerateArray()) {
                        var title = p.GetProperty("title").GetString();
                        if (title != null && !jsonLookup.ContainsKey(title)) {
                            // Clone the element or store it
                            jsonLookup[title] = p.Clone();
                        }
                    }
                }
            } catch (Exception ex) {
                return $"Error parsing problems.json: {ex.Message}";
            }

            // 2. Fetch problems from DB that need test cases (having less than 30 test cases)
            var dbProblems = await context.Problems
                .Include(p => p.TestCases)
                .ToListAsync();

            var pendingProblems = dbProblems
                .Where(p => p.TestCases.Count < 30)
                .Take(maxProblemsToProcess)
                .ToList();

            if (!pendingProblems.Any()) {
                return "All problems already have at least 30 test cases.";
            }

            int processedCount = 0;
            int totalGenerated = 0;
            var rand = new Random();

            foreach (var problem in pendingProblems) {
                if (!jsonLookup.TryGetValue(problem.Title, out var jsonProb)) {
                    continue;
                }

                // 3. Extract parameter info
                var metaData = jsonProb.GetProperty("metaData");
                var slug = jsonProb.GetProperty("slug").GetString();
                if (string.IsNullOrEmpty(slug)) continue;

                var paramsProp = metaData.GetProperty("params");
                var returnProp = metaData.GetProperty("return");
                var returnType = returnProp.GetProperty("type").GetString() ?? "integer";
                var methodName = metaData.GetProperty("name").GetString() ?? "solve";

                var paramTypes = new List<string>();
                foreach (var p in paramsProp.EnumerateArray()) {
                    paramTypes.Add(p.GetProperty("type").GetString() ?? "integer");
                }

                // 4. Fetch the raw Python solution
                string? pythonSolution = await FetchPythonSolutionAsync(httpClient, slug, problem.ProblemNumber, problem.Title);
                if (string.IsNullOrEmpty(pythonSolution)) {
                    continue;
                }

                // 5. Construct the full Python runner script
                string pythonScript = BuildPythonScript(pythonSolution, methodName, paramTypes, returnType);

                // 6. Generate new test cases
                // We clear existing test cases and re-generate: sample test cases + 35 new test cases
                // This ensures consistency.
                context.TestCases.RemoveRange(problem.TestCases);
                await context.SaveChangesAsync();

                var newTestCases = new List<TestCase>();

                // Add sample test cases from problems.json
                var sampleInputs = new List<string>();
                var sampleTestCasesArray = jsonProb.GetProperty("testCases");
                int paramCount = paramTypes.Count;
                if (paramCount <= 0) paramCount = 1;

                for (int i = 0; i < sampleTestCasesArray.GetArrayLength(); i += paramCount) {
                    var chunk = new List<string>();
                    for (int j = 0; j < paramCount && (i + j) < sampleTestCasesArray.GetArrayLength(); j++) {
                        var val = sampleTestCasesArray[i + j].ValueKind == JsonValueKind.String 
                            ? sampleTestCasesArray[i + j].GetString() 
                            : sampleTestCasesArray[i + j].GetRawText();
                        chunk.Add(val ?? "");
                    }
                    sampleInputs.Add(string.Join("\n", chunk));
                }

                // Extract expected outputs from description HTML
                var expectedOutputs = ExtractExpectedOutputs(problem.Description);

                for (int i = 0; i < sampleInputs.Count; i++) {
                    var expected = (i < expectedOutputs.Count) ? expectedOutputs[i] : "";
                    if (string.IsNullOrEmpty(expected)) {
                        try {
                            expected = await RunPythonLocallyOrOnJudgeAsync(pythonScript, sampleInputs[i], compilationService);
                        } catch {
                            expected = "";
                        }
                    }

                    newTestCases.Add(new TestCase {
                        Id = Guid.NewGuid(),
                        Input = sampleInputs[i],
                        ExpectedOutput = expected,
                        IsHidden = false,
                        ProblemId = problem.Id
                    });
                }

                // Generate 35 additional test cases
                // 5 edge cases, 15 small cases, 10 medium cases, 5 large cases
                var caseScales = new List<int>();
                for (int i = 0; i < 5; i++) caseScales.Add(0); // edge
                for (int i = 0; i < 15; i++) caseScales.Add(1); // small
                for (int i = 0; i < 10; i++) caseScales.Add(2); // medium
                for (int i = 0; i < 5; i++) caseScales.Add(3); // large

                int addedCount = 0;
                int attempts = 0;
                int maxAttempts = 150; // Safeguard against infinite loops

                while (addedCount < caseScales.Count && attempts < maxAttempts) {
                    attempts++;
                    int scale = caseScales[addedCount];
                    
                    // Generate input values
                    object[] inputs = GenerateInputs(problem.ProblemNumber, paramTypes, rand, scale);
                    
                    // Format input
                    var formattedLines = inputs.Select(val => {
                        if (val is string str) {
                            return $"\"{str}\"";
                        }
                        return JsonSerializer.Serialize(val);
                    }).ToList();
                    string stdin = string.Join("\n", formattedLines);

                    // Skip if duplicate input
                    if (newTestCases.Any(tc => tc.Input == stdin)) {
                        continue;
                    }

                    // Get expected output using our Python runner
                    try {
                        string expected = await RunPythonLocallyOrOnJudgeAsync(pythonScript, stdin, compilationService);
                        if (expected.StartsWith("ERROR:")) {
                            continue;
                        }

                        newTestCases.Add(new TestCase {
                            Id = Guid.NewGuid(),
                            Input = stdin,
                            ExpectedOutput = expected,
                            IsHidden = false,
                            ProblemId = problem.Id
                        });

                        addedCount++;
                    } catch {
                        // Skip failed executions (e.g. constraints violated inside python solution)
                        continue;
                    }
                }

                // Save to database
                context.TestCases.AddRange(newTestCases);
                await context.SaveChangesAsync();

                processedCount++;
                totalGenerated += newTestCases.Count;
            }

            return $"Success: Processed {processedCount} problems and generated {totalGenerated} total test cases.";
        }

        private static async Task<string?> FetchPythonSolutionAsync(HttpClient client, string slug, int problemNumber, string title) {
            // Attempt 1: Fetch from kamyu104 flat folder
            try {
                var url = $"https://raw.githubusercontent.com/kamyu104/LeetCode-Solutions/master/Python/{slug}.py";
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode) {
                    return await response.Content.ReadAsStringAsync();
                }
            } catch {}

            // Attempt 2: Fetch from doocs/leetcode structured folder
            try {
                var rangeFolder = $"{((problemNumber - 1) / 100 * 100):D4}-{(((problemNumber - 1) / 100 * 100) + 99):D4}";
                var problemFolder = $"{problemNumber:D4}.{title}";
                var url = $"https://raw.githubusercontent.com/doocs/leetcode/main/solution/{rangeFolder}/{Uri.EscapeDataString(problemFolder)}/Solution.py";
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode) {
                    return await response.Content.ReadAsStringAsync();
                }
            } catch {}

            return null;
        }

        private static string BuildPythonScript(string solutionCode, string methodName, List<string> paramTypes, string returnType) {
            var sb = new StringBuilder();
            sb.AppendLine(solutionCode);
            sb.AppendLine();
            sb.AppendLine("import sys");
            sb.AppendLine("import json");
            sb.AppendLine();
            sb.AppendLine("def parse_val(line, type_str):");
            sb.AppendLine("    line = line.strip()");
            sb.AppendLine("    if not line: return None");
            sb.AppendLine("    if type_str == 'integer':");
            sb.AppendLine("        return int(line)");
            sb.AppendLine("    elif type_str == 'double' or type_str == 'float':");
            sb.AppendLine("        return float(line)");
            sb.AppendLine("    elif type_str == 'boolean':");
            sb.AppendLine("        return line.lower() == 'true' or line == '1'");
            sb.AppendLine("    elif type_str == 'string':");
            sb.AppendLine("        if line.startswith('\"') and line.endswith('\"'):");
            sb.AppendLine("            return json.loads(line)");
            sb.AppendLine("        return line");
            sb.AppendLine("    elif type_str == 'character':");
            sb.AppendLine("        if (line.startswith('\"') and line.endswith('\"')) or (line.startswith(\"'\") and line.endswith(\"'\")):");
            sb.AppendLine("            return line[1:-1]");
            sb.AppendLine("        return line");
            sb.AppendLine("    else:");
            sb.AppendLine("        try:");
            sb.AppendLine("            return json.loads(line)");
            sb.AppendLine("        except:");
            sb.AppendLine("            return line");
            sb.AppendLine();
            sb.AppendLine("def format_output(result, type_str):");
            sb.AppendLine("    if result is None: return ''");
            sb.AppendLine("    if type_str == 'string' or type_str == 'character':");
            sb.AppendLine("        return str(result)");
            sb.AppendLine("    return json.dumps(result, separators=(',', ':'))");
            sb.AppendLine();
            sb.AppendLine("try:");
            sb.AppendLine("    lines = [line.strip() for line in sys.stdin if line.strip()]");
            sb.AppendLine("    params = []");
            
            for (int i = 0; i < paramTypes.Count; i++) {
                sb.AppendLine($"    if len(lines) > {i}:");
                sb.AppendLine($"        params.append(parse_val(lines[{i}], '{paramTypes[i]}'))");
            }
            
            sb.AppendLine("    sol = Solution()");
            sb.AppendLine("    method_name = '" + methodName + "'");
            sb.AppendLine("    if not hasattr(sol, method_name):");
            sb.AppendLine("        import re");
            sb.AppendLine("        snake = re.sub(r'(?<!^)(?=[A-Z])', '_', method_name).lower()");
            sb.AppendLine("        if hasattr(sol, snake):");
            sb.AppendLine("            method_name = snake");
            sb.AppendLine("        else:");
            sb.AppendLine("            attrs = [a for a in dir(sol) if not a.startswith('_')]");
            sb.AppendLine("            if attrs: method_name = attrs[0]");
            
            sb.AppendLine("    func = getattr(sol, method_name)");
            sb.AppendLine("    result = func(*params)");
            sb.AppendLine("    print(format_output(result, '" + returnType + "'))");
            sb.AppendLine("except Exception as e:");
            sb.AppendLine("    import traceback");
            sb.AppendLine("    print('ERROR:', str(e), file=sys.stderr)");
            sb.AppendLine("    traceback.print_exc(file=sys.stderr)");
            sb.AppendLine("    sys.exit(1)");
            
            return sb.ToString();
        }

        private static async Task<string> RunPythonLocallyOrOnJudgeAsync(string script, string stdin, ICompilationService compilationService) {
            try {
                // Try local execution
                var tempFile = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", $"temp_runner_{Guid.NewGuid():N}.py");
                await File.WriteAllTextAsync(tempFile, script);
                
                var psi = new ProcessStartInfo {
                    FileName = "python",
                    Arguments = $"\"{tempFile}\"",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = new Process { StartInfo = psi }) {
                    process.Start();
                    
                    await process.StandardInput.WriteAsync(stdin);
                    process.StandardInput.Close();
                    
                    string stdout = await process.StandardOutput.ReadToEndAsync();
                    string stderr = await process.StandardError.ReadToEndAsync();
                    
                    process.WaitForExit();
                    
                    try { File.Delete(tempFile); } catch {}
                    
                    if (process.ExitCode == 0) {
                        return stdout.Trim();
                    }
                }
            } catch {}
            
            // Fallback to Docker sandbox
            var tempTestCase = new TestCase {
                Id = Guid.NewGuid(),
                Input = stdin,
                ExpectedOutput = ""
            };
            var result = await compilationService.RunSampleAsync("python", script, tempTestCase, "", "");
            if (result.Passed || result.Status == "Accepted" || (result.Error == null && !string.IsNullOrEmpty(result.ActualOutput))) {
                return result.ActualOutput.Trim();
            }
            
            throw new InvalidOperationException($"Docker sandbox execution failed: {result.Error ?? result.Status}");
        }

        private static object[] GenerateInputs(int problemNumber, List<string> paramTypes, Random rand, int scale) {
            // Custom input overrides for special constraints
            if (problemNumber == 1) { // Two Sum
                int len = scale switch {
                    0 => rand.Next(2, 5),
                    1 => rand.Next(5, 15),
                    2 => rand.Next(15, 60),
                    _ => rand.Next(60, 200)
                };
                var nums = new List<int>();
                for (int i = 0; i < len; i++) nums.Add(rand.Next(-500, 500));
                
                int idx1 = rand.Next(0, len);
                int idx2 = rand.Next(0, len);
                while (idx2 == idx1 && len > 1) idx2 = rand.Next(0, len);
                
                int target = nums[idx1] + nums[idx2];
                return new object[] { nums.ToArray(), target };
            }

            if (problemNumber == 9) { // Palindrome Number
                if (rand.Next(0, 2) == 0) {
                    int halfVal = scale switch {
                        0 => rand.Next(0, 10),
                        1 => rand.Next(10, 100),
                        2 => rand.Next(100, 10000),
                        _ => rand.Next(10000, 1000000)
                    };
                    string s = halfVal.ToString();
                    string rev = new string(s.Reverse().ToArray());
                    return new object[] { int.Parse(s + rev) };
                } else {
                    return new object[] { rand.Next(-50, 10000000) };
                }
            }

            if (problemNumber == 20) { // Valid Parentheses
                var brackets = new[] { "()", "[]", "{}" };
                if (rand.Next(0, 2) == 0) { // Valid
                    var sb = new StringBuilder();
                    int depth = scale switch {
                        0 => 1,
                        1 => rand.Next(1, 4),
                        2 => rand.Next(4, 15),
                        _ => rand.Next(15, 50)
                    };
                    for (int i = 0; i < depth; i++) {
                        var b = brackets[rand.Next(brackets.Length)];
                        sb.Insert(sb.Length / 2, b);
                    }
                    return new object[] { sb.ToString() };
                } else { // Invalid
                    var chars = new[] { '(', ')', '[', ']', '{', '}' };
                    int len = scale switch {
                        0 => 1,
                        1 => rand.Next(2, 6),
                        2 => rand.Next(6, 20),
                        _ => rand.Next(20, 100)
                    };
                    var sb = new StringBuilder();
                    for (int i = 0; i < len; i++) sb.Append(chars[rand.Next(chars.Length)]);
                    return new object[] { sb.ToString() };
                }
            }

            if (problemNumber == 21) { // Merge Two Sorted Lists
                int len1 = scale switch { 0 => 0, 1 => rand.Next(1, 6), 2 => rand.Next(6, 20), _ => rand.Next(20, 100) };
                int len2 = scale switch { 0 => 0, 1 => rand.Next(1, 6), 2 => rand.Next(6, 20), _ => rand.Next(20, 100) };
                var l1 = new List<int>();
                var l2 = new List<int>();
                for (int i = 0; i < len1; i++) l1.Add(rand.Next(-100, 100));
                for (int i = 0; i < len2; i++) l2.Add(rand.Next(-100, 100));
                l1.Sort();
                l2.Sort();
                return new object[] { l1.ToArray(), l2.ToArray() };
            }

            // General case
            var results = new object[paramTypes.Count];
            for (int i = 0; i < paramTypes.Count; i++) {
                results[i] = GenerateRandomValue(paramTypes[i], rand, scale);
            }
            return results;
        }

        private static object GenerateRandomValue(string type, Random rand, int scale) {
            if (type == "integer") {
                if (scale == 0) return rand.Next(0, 2) == 0 ? 0 : 1;
                if (scale == 1) return rand.Next(-50, 50);
                if (scale == 2) return rand.Next(-1000, 1000);
                return rand.Next(-100000, 100000);
            }
            if (type == "double" || type == "float") {
                if (scale == 0) return 0.0;
                if (scale == 1) return (rand.NextDouble() - 0.5) * 10.0;
                if (scale == 2) return (rand.NextDouble() - 0.5) * 500.0;
                return (rand.NextDouble() - 0.5) * 50000.0;
            }
            if (type == "boolean") {
                return rand.Next(0, 2) == 0;
            }
            if (type == "character") {
                const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                return chars[rand.Next(chars.Length)].ToString();
            }
            if (type == "string") {
                if (scale == 0) return rand.Next(0, 2) == 0 ? "" : "a";
                int len = scale switch {
                    1 => rand.Next(1, 10),
                    2 => rand.Next(10, 50),
                    _ => rand.Next(50, 200)
                };
                const string chars = "abcdefghijklmnopqrstuvwxyz";
                var sb = new StringBuilder();
                for (int i = 0; i < len; i++) sb.Append(chars[rand.Next(chars.Length)]);
                return sb.ToString();
            }
            if (type.EndsWith("[]") || type.StartsWith("list<") || type.StartsWith("vector<")) {
                string elemType = "integer";
                if (type.Contains("string")) elemType = "string";
                else if (type.Contains("character") || type.Contains("char")) elemType = "character";
                else if (type.Contains("double") || type.Contains("float")) elemType = "double";
                else if (type.Contains("boolean") || type.Contains("bool")) elemType = "boolean";

                if (scale == 0) return rand.Next(0, 2) == 0 ? new object[0] : new object[] { GenerateRandomValue(elemType, rand, 0) };

                int count = scale switch {
                    1 => rand.Next(1, 10),
                    2 => rand.Next(10, 40),
                    _ => rand.Next(40, 150)
                };

                var arr = new object[count];
                for (int i = 0; i < count; i++) {
                    arr[i] = GenerateRandomValue(elemType, rand, scale);
                }
                return arr;
            }
            if (type.Contains("matrix") || type.Contains("[][]") || type.Contains("vector<vector<")) {
                string elemType = "integer";
                if (type.Contains("string")) elemType = "string";
                
                if (scale == 0) return new object[0][];
                
                int rows = scale switch {
                    1 => rand.Next(1, 4),
                    2 => rand.Next(4, 8),
                    _ => rand.Next(8, 15)
                };
                int cols = scale switch {
                    1 => rand.Next(1, 4),
                    2 => rand.Next(4, 8),
                    _ => rand.Next(8, 15)
                };
                
                var matrix = new object[rows][];
                for (int r = 0; r < rows; r++) {
                    matrix[r] = new object[cols];
                    for (int c = 0; c < cols; c++) {
                        matrix[r][c] = GenerateRandomValue(elemType, rand, scale);
                    }
                }
                return matrix;
            }
            
            return 0;
        }

        private static List<string> ExtractExpectedOutputs(string htmlContent) {
            var outputs = new List<string>();
            if (string.IsNullOrWhiteSpace(htmlContent)) return outputs;

            var matches = System.Text.RegularExpressions.Regex.Matches(htmlContent, 
                @"Output:<\/strong>\s*(?:<span\s+class=""example-io"">|<pre>|<code>)?(.*?)(?:<\/span>|<\/pre>|<\/code>|(?=<\/p>)|(?=<p>)|(?=\n)|(?=<div))", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (System.Text.RegularExpressions.Match match in matches) {
                var val = match.Groups[1].Value;
                val = System.Text.RegularExpressions.Regex.Replace(val, "<.*?>", "").Trim();
                val = System.Net.WebUtility.HtmlDecode(val);

                if (val.StartsWith("\"") && val.EndsWith("\"") && val.Length >= 2) {
                    val = val.Substring(1, val.Length - 2);
                }

                outputs.Add(val);
            }

            return outputs;
        }
    }
}
