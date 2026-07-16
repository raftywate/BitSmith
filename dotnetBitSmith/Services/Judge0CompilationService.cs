using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Entities.Enums;
using dotnetBitSmith.Models.Submissions;
using dotnetBitSmith.Models.Judge0;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.IO;

namespace dotnetBitSmith.Services {
    public class Judge0CompilationService : ICompilationService {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<Judge0CompilationService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _judge0ApiUrl;
        private readonly string _judge0ApiKey;
        private readonly string _judge0ApiHost;

        public Judge0CompilationService(
            ApplicationDbContext context,
            ILogger<Judge0CompilationService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory) {
            _context = context;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _judge0ApiUrl = configuration["Judge0Settings:ApiUrl"] ?? "http://localhost:2358";
            _judge0ApiKey = configuration["Judge0Settings:ApiKey"] ?? "";
            
            if (Uri.TryCreate(_judge0ApiUrl, UriKind.Absolute, out var uri)) {
                _judge0ApiHost = uri.Host;
            } else {
                _judge0ApiHost = "localhost";
            }
        }

        public async Task<Submission> JudgeSubmissionAsync(Submission submission) {
            _logger.LogInformation("Starting judgment for Submission {SubmissionId} using Docker sandbox", submission.Id);

            try {
                var problem = await _context.Problems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == submission.ProblemId);

                if (problem == null) {
                    throw new InvalidOperationException($"Problem with ID {submission.ProblemId} not found.");
                }

                var testCases = await _context.TestCases
                    .AsNoTracking()
                    .Where(tc => tc.ProblemId == submission.ProblemId)
                    .OrderBy(tc => tc.Id)
                    .ToListAsync();

                if (!testCases.Any()) {
                    _logger.LogWarning("No test cases found for problem {ProblemId}.", submission.ProblemId);
                    submission.Status = SubmissionStatus.WrongAnswer;
                    submission.ErrorMessage = "No test cases configured.";
                    _context.Submissions.Update(submission);
                    await _context.SaveChangesAsync();
                    return submission;
                }

                var wrappedCode = submission.Code;
                var metadata = await GetProblemMetadataAsync(problem.Title);
                if (metadata != null) {
                    wrappedCode = WrapCode(submission.Language, submission.Code, metadata.Value.MethodName, metadata.Value.ParamTypes, metadata.Value.ReturnType);
                } else {
                    wrappedCode = InjectLibraries(submission.Language, submission.Code);
                }

                var combinedInput = string.Join("\n", testCases.Select(tc => tc.Input.TrimEnd('\r', '\n')));
                var result = await ExecuteInSandboxAsync(submission.Language, wrappedCode, combinedInput);

                submission.ErrorMessage = result.Error;
                submission.ExecutionTimeMs = result.ExecutionTimeMs;
                submission.ExecutionMemoryKb = null; // Memory tracking not supported in basic Phase 1

                submission.TotalCount = testCases.Count;
                submission.PassedCount = 0;
                submission.FailedTestCaseInput = null;
                submission.FailedTestCaseExpected = null;
                submission.FailedTestCaseActual = null;

                if (result.Status == "Timeout") {
                    submission.Status = SubmissionStatus.TimeLimitExceeded;
                } else if (result.Status == "CompileError") {
                    submission.Status = SubmissionStatus.CompilationError;
                } else if (result.Status == "RuntimeError") {
                    submission.Status = SubmissionStatus.RuntimeError;
                } else {
                    // Successful run. Let's compare output for each test case
                    var actualOutputs = (result.Stdout ?? string.Empty)
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrEmpty(line))
                        .ToList();

                    bool allowAnyOrder = problem.Description?.Contains("any order", StringComparison.OrdinalIgnoreCase) ?? false;
                    var finalStatus = SubmissionStatus.Accepted;
                    int passed = 0;

                    for (int i = 0; i < testCases.Count; i++) {
                        if (i >= actualOutputs.Count) {
                            finalStatus = SubmissionStatus.WrongAnswer;
                            submission.FailedTestCaseInput = testCases[i].Input;
                            submission.FailedTestCaseExpected = testCases[i].ExpectedOutput;
                            submission.FailedTestCaseActual = "";
                            _logger.LogWarning("TestCase {TestCaseId}: No output received. Expected: '{Expected}'", testCases[i].Id, testCases[i].ExpectedOutput);
                            break;
                        }

                        var actualOutput = NormalizeOutput(actualOutputs[i], allowAnyOrder);
                        var expectedOutput = NormalizeOutput(testCases[i].ExpectedOutput, allowAnyOrder);

                        if (!string.Equals(actualOutput, expectedOutput, StringComparison.Ordinal)) {
                            finalStatus = SubmissionStatus.WrongAnswer;
                            submission.FailedTestCaseInput = testCases[i].Input;
                            submission.FailedTestCaseExpected = testCases[i].ExpectedOutput;
                            submission.FailedTestCaseActual = actualOutputs[i];
                            _logger.LogInformation("Wrong Answer on TestCase {TestCaseId}. Expected: '{Expected}', Actual: '{Actual}'", testCases[i].Id, expectedOutput, actualOutput);
                            break;
                        }
                        passed++;
                    }

                    submission.PassedCount = passed;
                    submission.Status = finalStatus;
                }

                _context.Submissions.Update(submission);
                await _context.SaveChangesAsync();
                return submission;

            } catch (Exception ex) {
                _logger.LogError(ex, "Error during judgment for Submission {SubmissionId}", submission.Id);
                submission.Status = SubmissionStatus.InternalError;
                submission.ErrorMessage = ex.Message;
                _context.Submissions.Update(submission);
                await _context.SaveChangesAsync();
                return submission;
            }
        }

        public async Task<SampleRunResultModel> RunSampleAsync(string language, string code, TestCase testCase, string problemTitle, string problemDescription) {
            var wrappedCode = code;
            bool allowAnyOrder = false;

            try {
                if (!string.IsNullOrEmpty(problemTitle)) {
                    allowAnyOrder = problemDescription?.Contains("any order", StringComparison.OrdinalIgnoreCase) ?? false;
                    var metadata = await GetProblemMetadataAsync(problemTitle);
                    if (metadata != null) {
                        wrappedCode = WrapCode(language, code, metadata.Value.MethodName, metadata.Value.ParamTypes, metadata.Value.ReturnType);
                    } else {
                        wrappedCode = InjectLibraries(language, code);
                    }
                } else {
                    wrappedCode = InjectLibraries(language, code);
                }
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to wrap code for sample run.");
            }

            var result = await ExecuteInSandboxAsync(language, wrappedCode, testCase.Input);
            var actualOutput = NormalizeOutput(result.Stdout ?? string.Empty, allowAnyOrder);
            var expectedOutput = NormalizeOutput(testCase.ExpectedOutput, allowAnyOrder);

            var isPassed = result.Status == "Success" && string.Equals(actualOutput, expectedOutput, StringComparison.Ordinal);

            return new SampleRunResultModel {
                TestCaseId = testCase.Id,
                Input = testCase.Input,
                ExpectedOutput = testCase.ExpectedOutput,
                ActualOutput = actualOutput,
                Status = result.Status == "Success" ? (isPassed ? "Accepted" : "Wrong Answer") : result.Status,
                Error = result.Error,
                ExecutionTimeMs = result.ExecutionTimeMs,
                ExecutionMemoryKb = null,
                Passed = isPassed
            };
        }

        public async Task<IEnumerable<SampleRunResultModel>> RunSamplesAsync(string language, string code, List<TestCase> testCases, string problemTitle, string problemDescription) {
            var wrappedCode = code;
            bool allowAnyOrder = false;

            try {
                if (!string.IsNullOrEmpty(problemTitle)) {
                    allowAnyOrder = problemDescription?.Contains("any order", StringComparison.OrdinalIgnoreCase) ?? false;
                    var metadata = await GetProblemMetadataAsync(problemTitle);
                    if (metadata != null) {
                        wrappedCode = WrapCode(language, code, metadata.Value.MethodName, metadata.Value.ParamTypes, metadata.Value.ReturnType);
                    } else {
                        wrappedCode = InjectLibraries(language, code);
                    }
                } else {
                    wrappedCode = InjectLibraries(language, code);
                }
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to wrap code for samples run.");
            }

            var combinedInput = string.Join("\n", testCases.Select(tc => tc.Input.TrimEnd('\r', '\n')));
            var result = await ExecuteInSandboxAsync(language, wrappedCode, combinedInput);

            var actualOutputs = (result.Stdout ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToList();

            var runResults = new List<SampleRunResultModel>();
            for (int i = 0; i < testCases.Count; i++) {
                var tc = testCases[i];
                var expectedOutput = NormalizeOutput(tc.ExpectedOutput, allowAnyOrder);
                string actualOutput = "";
                bool isPassed = false;
                string status = result.Status;

                if (result.Status == "Success") {
                    if (i < actualOutputs.Count) {
                        actualOutput = NormalizeOutput(actualOutputs[i], allowAnyOrder);
                        isPassed = string.Equals(actualOutput, expectedOutput, StringComparison.Ordinal);
                        status = isPassed ? "Accepted" : "Wrong Answer";
                    } else {
                        status = "Wrong Answer";
                    }
                }

                runResults.Add(new SampleRunResultModel {
                    TestCaseId = tc.Id,
                    Input = tc.Input,
                    ExpectedOutput = tc.ExpectedOutput,
                    ActualOutput = actualOutput,
                    Status = status,
                    Error = result.Error,
                    ExecutionTimeMs = result.ExecutionTimeMs,
                    ExecutionMemoryKb = null,
                    Passed = isPassed
                });
            }

            return runResults;
        }

        public async Task<RunCodeResultModel> ExecuteCustomCodeAsync(string language, string code, string stdin) {
            var wrappedCode = InjectLibraries(language, code);
            var result = await ExecuteInSandboxAsync(language, wrappedCode, stdin);
            return new RunCodeResultModel {
                Stdout = result.Stdout ?? string.Empty,
                Stderr = result.Error ?? string.Empty,
                Status = result.Status,
                ExecutionTimeMs = result.ExecutionTimeMs
            };
        }

        private async Task<bool> IsWarmContainerRunningAsync(string containerName) {
            try {
                var result = await RunProcessAsync("docker", "inspect -f \"{{.State.Running}}\" " + containerName, 2000);
                return result.ExitCode == 0 && result.Stdout.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
            } catch {
                return false;
            }
        }

        public async Task<SandboxResult> ExecuteInSandboxAsync(string language, string wrappedCode, string stdin) {
            _logger.LogInformation("Sending execution request to Judge0...");
            try {
                int languageId = GetLanguageId(language);
                
                var createRequest = new Judge0CreateSubmissionRequest {
                    LanguageId = languageId,
                    SourceCode = wrappedCode,
                    StandardInputs = stdin
                };

                var client = _httpClientFactory.CreateClient();
                
                if (!string.IsNullOrEmpty(_judge0ApiKey)) {
                    client.DefaultRequestHeaders.Add("X-RapidAPI-Key", _judge0ApiKey);
                    client.DefaultRequestHeaders.Add("X-RapidAPI-Host", _judge0ApiHost);
                }

                var jsonRequest = JsonSerializer.Serialize(createRequest);
                var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var httpResponse = await client.PostAsync($"{_judge0ApiUrl}/submissions?base64_encoded=false&wait=false", httpContent);

                if (!httpResponse.IsSuccessStatusCode) {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Judge0 failed to create submission. Status: {StatusCode}, Body: {ErrorBody}", httpResponse.StatusCode, errorContent);
                    return new SandboxResult {
                        Status = "RuntimeError",
                        Error = $"Failed to create submission with Judge0. Status: {httpResponse.StatusCode}"
                    };
                }

                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                var createResponse = JsonSerializer.Deserialize<Judge0CreateSubmissionResponse>(responseContent);
                if (createResponse == null || string.IsNullOrEmpty(createResponse.Token)) {
                    return new SandboxResult {
                        Status = "RuntimeError",
                        Error = "Judge0 returned an invalid token."
                    };
                }

                var judgeToken = createResponse.Token;
                _logger.LogInformation("Submission created on Judge0 with token {JudgeToken}", judgeToken);

                Judge0GetSubmissionResponse? finalJudgeResponse = null;
                int pollAttempts = 0;

                while (true) {
                    pollAttempts++;
                    if (pollAttempts > 60) { // Failsafe: 45 seconds
                        return new SandboxResult {
                            Status = "Timeout",
                            Error = "Polling Judge0 timed out."
                        };
                    }
                    await Task.Delay(750);

                    var getResponse = await client.GetAsync($"{_judge0ApiUrl}/submissions/{judgeToken}?base64_encoded=false");
                    if (!getResponse.IsSuccessStatusCode) {
                        return new SandboxResult {
                            Status = "RuntimeError",
                            Error = "Failed to poll for submission result from Judge0."
                        };
                    }

                    var getResponseContent = await getResponse.Content.ReadAsStringAsync();
                    finalJudgeResponse = JsonSerializer.Deserialize<Judge0GetSubmissionResponse>(getResponseContent);

                    if (finalJudgeResponse == null) {
                        return new SandboxResult {
                            Status = "RuntimeError",
                            Error = "Judge0 returned a null polling response."
                        };
                    }

                    if (finalJudgeResponse.Status.Id > 2) {
                        break;
                    }
                }

                var result = new SandboxResult();
                
                if (double.TryParse(finalJudgeResponse.Time?.Replace("s", ""), out double timeInSeconds)) {
                    result.ExecutionTimeMs = (int)(timeInSeconds * 1000);
                }

                if (finalJudgeResponse.Status.Id == 3 || finalJudgeResponse.Status.Id == 4) {
                    result.Status = "Success";
                    result.Stdout = finalJudgeResponse.StandardOutput;
                } else if (finalJudgeResponse.Status.Id == 5) {
                    result.Status = "Timeout";
                    result.Error = "Time Limit Exceeded";
                } else if (finalJudgeResponse.Status.Id == 6) {
                    result.Status = "CompileError";
                    result.Error = finalJudgeResponse.CompileOutput ?? finalJudgeResponse.StandardError;
                } else {
                    result.Status = "RuntimeError";
                    result.Error = finalJudgeResponse.StandardError ?? finalJudgeResponse.CompileOutput ?? finalJudgeResponse.Status.Description;
                }

                return result;

            } catch (Exception ex) {
                _logger.LogError(ex, "Error executing code via Judge0");
                return new SandboxResult {
                    Status = "RuntimeError",
                    Error = ex.Message
                };
            }
        }

        private static int GetLanguageId(string language) {
            switch (NormalizeLanguage(language)) {
                case "python":
                    return 71;
                case "cpp":
                    return 54;
                case "c":
                    return 50;
                case "java":
                    return 62;
                case "csharp":
                    return 51;
                default:
                    throw new NotSupportedException($"Language '{language}' is not supported.");
            }
        }

        private async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(string fileName, string arguments, int timeoutMs) {
            using (var process = new Process()) {
                process.StartInfo = new ProcessStartInfo {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var stdoutBuilder = new StringBuilder();
                var stderrBuilder = new StringBuilder();

                using (var outputWaitHandle = new AutoResetEvent(false))
                using (var errorWaitHandle = new AutoResetEvent(false)) {
                    process.OutputDataReceived += (sender, e) => {
                        if (e.Data == null) outputWaitHandle.Set();
                        else stdoutBuilder.AppendLine(e.Data);
                    };
                    process.ErrorDataReceived += (sender, e) => {
                        if (e.Data == null) errorWaitHandle.Set();
                        else stderrBuilder.AppendLine(e.Data);
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (process.WaitForExit(timeoutMs)) {
                        outputWaitHandle.WaitOne(1000);
                        errorWaitHandle.WaitOne(1000);
                        return (process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
                    } else {
                        try { process.Kill(true); } catch { }
                        return (-2, string.Empty, "Timeout");
                    }
                }
            }
        }

        private void CleanupContainer(string containerName) {
            try {
                var psi = new ProcessStartInfo {
                    FileName = "docker",
                    Arguments = $"rm -f {containerName}",
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi)) {
                    p?.WaitForExit(2000);
                }
            } catch { }
        }

        private static readonly SemaphoreSlim _metadataSemaphore = new SemaphoreSlim(1, 1);

        private async Task<(string MethodName, List<string> ParamTypes, string ReturnType)?> GetProblemMetadataAsync(string problemTitle) {
            await _metadataSemaphore.WaitAsync();
            try {
                var jsonOptions = new JsonDocumentOptions {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };

                // First check database for custom metadata
                var problem = await _context.Problems.AsNoTracking().FirstOrDefaultAsync(p => p.Title == problemTitle);
                if (problem != null && !string.IsNullOrWhiteSpace(problem.MetaDataJson)) {
                    using (var doc = JsonDocument.Parse(problem.MetaDataJson, jsonOptions)) {
                        var metaData = doc.RootElement;
                        var name = metaData.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "solve" : "solve";
                        var returnType = "integer";
                        if (metaData.TryGetProperty("return", out var retProp)) {
                            if (retProp.ValueKind == JsonValueKind.String) {
                                returnType = retProp.GetString() ?? "integer";
                            } else if (retProp.ValueKind == JsonValueKind.Object && retProp.TryGetProperty("type", out var typeProp)) {
                                returnType = typeProp.GetString() ?? "integer";
                            }
                        }
                        var paramTypes = new List<string>();
                        if (metaData.TryGetProperty("params", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Array) {
                            foreach (var param in paramsProp.EnumerateArray()) {
                                if (param.TryGetProperty("type", out var pType)) {
                                    paramTypes.Add(pType.GetString() ?? "integer");
                                } else {
                                    paramTypes.Add("integer");
                                }
                            }
                        }
                        return (name, paramTypes, returnType);
                    }
                }

                // Fallback to problems.json
                var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "problems.json");
                if (!File.Exists(jsonPath)) return null;

                var jsonString = await File.ReadAllTextAsync(jsonPath);
                using (var doc = JsonDocument.Parse(jsonString, jsonOptions)) {
                    var problemsArray = doc.RootElement.GetProperty("problems");
                    foreach (var p in problemsArray.EnumerateArray()) {
                        var title = p.GetProperty("title").GetString();
                        if (string.Equals(title, problemTitle, StringComparison.OrdinalIgnoreCase)) {
                            var metaData = p.GetProperty("metaData");
                            var name = metaData.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "solve" : "solve";
                            var returnType = "integer";
                            if (metaData.TryGetProperty("return", out var retProp)) {
                                if (retProp.ValueKind == JsonValueKind.String) {
                                    returnType = retProp.GetString() ?? "integer";
                                } else if (retProp.ValueKind == JsonValueKind.Object && retProp.TryGetProperty("type", out var typeProp)) {
                                    returnType = typeProp.GetString() ?? "integer";
                                }
                            }
                            var paramTypes = new List<string>();
                            if (metaData.TryGetProperty("params", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Array) {
                                foreach (var param in paramsProp.EnumerateArray()) {
                                    if (param.TryGetProperty("type", out var pType)) {
                                        paramTypes.Add(pType.GetString() ?? "integer");
                                    } else {
                                        paramTypes.Add("integer");
                                    }
                                }
                            }
                            return (name, paramTypes, returnType);
                        }
                    }
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to read problem metadata for title {Title}", problemTitle);
            } finally {
                _metadataSemaphore.Release();
            }
            return null;
        }

        private static string WrapCode(string language, string userCode, string methodName, List<string> paramTypes, string returnType) {
            switch (NormalizeLanguage(language)) {
                case "cpp":
                    return WrapCpp(userCode, methodName, paramTypes, returnType);
                case "c":
                    return WrapC(userCode, methodName, paramTypes, returnType);
                case "python":
                    return WrapPython(userCode, methodName, paramTypes, returnType);
                case "csharp":
                    return WrapCsharp(userCode, methodName, paramTypes, returnType);
                case "java":
                    return WrapJava(userCode, methodName, paramTypes, returnType);
                default:
                    return userCode;
            }
        }

        private static string InjectLibraries(string language, string userCode) {
            switch (NormalizeLanguage(language)) {
                case "cpp":
                    return "#include <bits/stdc++.h>\nusing namespace std;\n\nstruct ListNode {\n    int val;\n    ListNode *next;\n    ListNode() : val(0), next(nullptr) {}\n    ListNode(int x) : val(x), next(nullptr) {}\n    ListNode(int x, ListNode *next) : val(x), next(next) {}\n};\n\nstruct TreeNode {\n    int val;\n    TreeNode *left;\n    TreeNode *right;\n    TreeNode() : val(0), left(nullptr), right(nullptr) {}\n    TreeNode(int x) : val(x), left(nullptr), right(nullptr) {}\n    TreeNode(int x, TreeNode *left, TreeNode *right) : val(x), left(left), right(right) {}\n};\n\n" + userCode;
                case "c":
                    return "#define _GNU_SOURCE\n#include <stdio.h>\n#include <stdlib.h>\n#include <string.h>\n#include <stdbool.h>\n#include <math.h>\n#include <limits.h>\n#include <ctype.h>\n\nstruct ListNode {\n    int val;\n    struct ListNode *next;\n};\n\nstruct TreeNode {\n    int val;\n    struct TreeNode *left;\n    struct TreeNode *right;\n};\n\n" + userCode;
                case "java":
                    return "import java.util.*;\nimport java.io.*;\nimport java.math.*;\nimport java.text.*;\n\nclass ListNode {\n    int val;\n    ListNode next;\n    ListNode() {}\n    ListNode(int val) { this.val = val; }\n    ListNode(int val, ListNode next) { this.val = val; this.next = next; }\n}\n\nclass TreeNode {\n    int val;\n    TreeNode left;\n    TreeNode right;\n    TreeNode() {}\n    TreeNode(int val) { this.val = val; }\n    TreeNode(int val, TreeNode left, TreeNode right) {\n        this.val = val;\n        this.left = left;\n        this.right = right;\n    }\n}\n\n" + userCode;
                case "csharp":
                    return "using System;\nusing System.Collections;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Text;\nusing System.Text.RegularExpressions;\nusing System.Threading.Tasks;\nusing System.Numerics;\n\npublic class ListNode {\n    public int val;\n    public ListNode next;\n    public ListNode(int val=0, ListNode next=null) {\n        this.val = val;\n        this.next = next;\n    }\n}\n\npublic class TreeNode {\n    public int val;\n    public TreeNode left;\n    public TreeNode right;\n    public TreeNode(int val=0, TreeNode left=null, TreeNode right=null) {\n        this.val = val;\n        this.left = left;\n        this.right = right;\n    }\n}\n\n" + userCode;
                case "python":
                    return "import sys\nimport os\nimport math\nimport collections\nimport itertools\nimport functools\nimport bisect\nimport heapq\nimport re\nimport string\nimport operator\nimport copy\nfrom typing import *\n\nclass ListNode:\n    def __init__(self, val=0, next=None):\n        self.val = val\n        self.next = next\n\nclass TreeNode:\n    def __init__(self, val=0, left=None, right=None):\n        self.val = val\n        self.left = left\n        self.right = right\n\n" + userCode;
                default:
                    return userCode;
            }
        }

        private static string GetCppType(string jsonType) {
            if (jsonType.EndsWith("[]")) return "vector<" + GetCppType(jsonType.Substring(0, jsonType.Length - 2)) + ">";
            switch(jsonType) {
                case "integer": return "int";
                case "long": return "long long";
                case "double": return "double";
                case "float": return "float";
                case "boolean": return "bool";
                case "character": return "char";
                case "string": return "string";
                case "ListNode": return "ListNode*";
                case "TreeNode": return "TreeNode*";
                default: return "int";
            }
        }

        private static string GetCppParser(string jsonType, string varLine) {
            if (jsonType == "ListNode") return $"BitSmithRunner::parse_list_node({varLine})";
            if (jsonType == "TreeNode") return $"BitSmithRunner::parse_tree_node({varLine})";
            if (jsonType == "integer") return $"BitSmithRunner::parse_int({varLine})";
            if (jsonType == "long") return $"BitSmithRunner::parse_long({varLine})";
            if (jsonType == "double") return $"BitSmithRunner::parse_double({varLine})";
            if (jsonType == "float") return $"BitSmithRunner::parse_double({varLine})"; // C++ uses double for stof/stod usually, let's cast or rely on implicit conv
            if (jsonType == "boolean") return $"BitSmithRunner::parse_bool({varLine})";
            if (jsonType == "character") return $"BitSmithRunner::parse_char({varLine})";
            if (jsonType == "string") return $"BitSmithRunner::parse_string({varLine})";
            
            if (jsonType.EndsWith("[][]")) {
                string baseType = jsonType.Substring(0, jsonType.Length - 4);
                string parserFn = GetCppParser(baseType, "").Replace("(", "").Replace(")", "");
                return $"BitSmithRunner::parse_matrix<{GetCppType(baseType)}>({varLine}, {parserFn})";
            } else if (jsonType.EndsWith("[]")) {
                string baseType = jsonType.Substring(0, jsonType.Length - 2);
                string parserFn = GetCppParser(baseType, "").Replace("(", "").Replace(")", "");
                return $"BitSmithRunner::parse_vector<{GetCppType(baseType)}>({varLine}, {parserFn})";
            }
            return $"/* Unsupported {jsonType} */ 0";
        }

        private static string WrapCpp(string userCode, string methodName, List<string> paramTypes, string returnType) {
            var sb = new StringBuilder();
            sb.AppendLine("#include <bits/stdc++.h>");
            sb.AppendLine();
            sb.AppendLine("using namespace std;");
            sb.AppendLine();
            sb.AppendLine("struct ListNode {");
            sb.AppendLine("    int val;");
            sb.AppendLine("    ListNode *next;");
            sb.AppendLine("    ListNode() : val(0), next(nullptr) {}");
            sb.AppendLine("    ListNode(int x) : val(x), next(nullptr) {}");
            sb.AppendLine("    ListNode(int x, ListNode *next) : val(x), next(next) {}");
            sb.AppendLine("};");
            sb.AppendLine();
            sb.AppendLine("struct TreeNode {");
            sb.AppendLine("    int val;");
            sb.AppendLine("    TreeNode *left;");
            sb.AppendLine("    TreeNode *right;");
            sb.AppendLine("    TreeNode() : val(0), left(nullptr), right(nullptr) {}");
            sb.AppendLine("    TreeNode(int x) : val(x), left(nullptr), right(nullptr) {}");
            sb.AppendLine("    TreeNode(int x, TreeNode *left, TreeNode *right) : val(x), left(left), right(right) {}");
            sb.AppendLine("};");
            sb.AppendLine();
            sb.AppendLine(userCode);
            sb.AppendLine();
            sb.AppendLine("namespace BitSmithRunner {");
            sb.AppendLine("    string trim(const string& str) {");
            sb.AppendLine("        size_t first = str.find_first_not_of(\" \\t\\r\\n\");");
            sb.AppendLine("        if (first == string::npos) return \"\";");
            sb.AppendLine("        size_t last = str.find_last_not_of(\" \\t\\r\\n\");");
            sb.AppendLine("        return str.substr(first, (last - first + 1));");
            sb.AppendLine("    }");
            sb.AppendLine("    int parse_int(const string& s) { return stoi(trim(s)); }");
            sb.AppendLine("    double parse_double(const string& s) { return stod(trim(s)); }");
            sb.AppendLine("    bool parse_bool(const string& raw_s) { string s = trim(raw_s); return s == \"true\" || s == \"1\"; }");
            sb.AppendLine("    string parse_string(const string& raw_s) {");
            sb.AppendLine("        string s = trim(raw_s);");
            sb.AppendLine("        if (s.length() >= 2 && s.front() == '\"' && s.back() == '\"') return s.substr(1, s.length() - 2);");
            sb.AppendLine("        return s;");
            sb.AppendLine("    }");
            sb.AppendLine("    char parse_char(const string& s) {");
            sb.AppendLine("        string val = parse_string(s);");
            sb.AppendLine("        return val.empty() ? ' ' : val[0];");
            sb.AppendLine("    }");
            sb.AppendLine("    vector<string> splitJsonArray(const string& s_raw) {");
            sb.AppendLine("        string t = trim(s_raw);");
            sb.AppendLine("        if (t.length() < 2) return {};");
            sb.AppendLine("        t = trim(t.substr(1, t.length() - 2));");
            sb.AppendLine("        if (t.empty()) return {};");
            sb.AppendLine("        vector<string> list;");
            sb.AppendLine("        int depth = 0;");
            sb.AppendLine("        string curr = \"\";");
            sb.AppendLine("        bool inQuotes = false;");
            sb.AppendLine("        for (size_t i = 0; i < t.length(); i++) {");
            sb.AppendLine("            char c = t[i];");
            sb.AppendLine("            if (c == '\"' && (i == 0 || t[i-1] != '\\\\')) inQuotes = !inQuotes;");
            sb.AppendLine("            if (!inQuotes) {");
            sb.AppendLine("                if (c == '[') depth++;");
            sb.AppendLine("                else if (c == ']') depth--;");
            sb.AppendLine("                else if (c == ',' && depth == 0) {");
            sb.AppendLine("                    list.push_back(trim(curr));");
            sb.AppendLine("                    curr = \"\";");
            sb.AppendLine("                    continue;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            curr += c;");
            sb.AppendLine("        }");
            sb.AppendLine("        list.push_back(trim(curr));");
            sb.AppendLine("        return list;");
            sb.AppendLine("    }");
            sb.AppendLine("    template<typename T>");
            sb.AppendLine("    vector<T> parse_vector(const string& raw_s, T(*parser)(const string&)) {");
            sb.AppendLine("        vector<string> parts = splitJsonArray(raw_s);");
            sb.AppendLine("        vector<T> res;");
            sb.AppendLine("        for (const string& p : parts) res.push_back(parser(p));");
            sb.AppendLine("        return res;");
            sb.AppendLine("    }");
            sb.AppendLine("    template<typename T>");
            sb.AppendLine("    vector<vector<T>> parse_matrix(const string& raw_s, T(*parser)(const string&)) {");
            sb.AppendLine("        vector<string> parts = splitJsonArray(raw_s);");
            sb.AppendLine("        vector<vector<T>> res;");
            sb.AppendLine("        for (const string& p : parts) res.push_back(parse_vector<T>(p, parser));");
            sb.AppendLine("        return res;");
            sb.AppendLine("    }");
            sb.AppendLine("    long long parse_long(const string& s) { return stoll(trim(s)); }");
            sb.AppendLine("    ListNode* parse_list_node(const string& raw_s) {");
            sb.AppendLine("        string s = trim(raw_s);");
            sb.AppendLine("        if (s.length() < 2 || s.front() != '[' || s.back() != ']') return nullptr;");
            sb.AppendLine("        string content = s.substr(1, s.length() - 2);");
            sb.AppendLine("        if (content.empty()) return nullptr;");
            sb.AppendLine("        stringstream ss(content);");
            sb.AppendLine("        string token;");
            sb.AppendLine("        ListNode dummy(0);");
            sb.AppendLine("        ListNode* tail = &dummy;");
            sb.AppendLine("        while (getline(ss, token, ',')) {");
            sb.AppendLine("            tail->next = new ListNode(stoi(trim(token)));");
            sb.AppendLine("            tail = tail->next;");
            sb.AppendLine("        }");
            sb.AppendLine("        return dummy.next;");
            sb.AppendLine("    }");
            sb.AppendLine("    TreeNode* parse_tree_node(const string& raw_s) {");
            sb.AppendLine("        string s = trim(raw_s);");
            sb.AppendLine("        if (s.length() < 2 || s.front() != '[' || s.back() != ']') return nullptr;");
            sb.AppendLine("        string content = s.substr(1, s.length() - 2);");
            sb.AppendLine("        if (content.empty()) return nullptr;");
            sb.AppendLine("        vector<string> tokens;");
            sb.AppendLine("        stringstream ss(content);");
            sb.AppendLine("        string token;");
            sb.AppendLine("        while (getline(ss, token, ',')) {");
            sb.AppendLine("            tokens.push_back(trim(token));");
            sb.AppendLine("        }");
            sb.AppendLine("        if (tokens.empty() || tokens[0] == \"null\" || tokens[0].empty()) return nullptr;");
            sb.AppendLine("        TreeNode* root = new TreeNode(stoi(tokens[0]));");
            sb.AppendLine("        queue<TreeNode*> q;");
            sb.AppendLine("        q.push(root);");
            sb.AppendLine("        size_t i = 1;");
            sb.AppendLine("        while (!q.empty() && i < tokens.size()) {");
            sb.AppendLine("            TreeNode* curr = q.front();");
            sb.AppendLine("            q.pop();");
            sb.AppendLine("            if (i < tokens.size()) {");
            sb.AppendLine("                if (!tokens[i].empty() && tokens[i] != \"null\") {");
            sb.AppendLine("                    curr->left = new TreeNode(stoi(tokens[i]));");
            sb.AppendLine("                    q.push(curr->left);");
            sb.AppendLine("                }");
            sb.AppendLine("                i++;");
            sb.AppendLine("            }");
            sb.AppendLine("            if (i < tokens.size()) {");
            sb.AppendLine("                if (!tokens[i].empty() && tokens[i] != \"null\") {");
            sb.AppendLine("                    curr->right = new TreeNode(stoi(tokens[i]));");
            sb.AppendLine("                    q.push(curr->right);");
            sb.AppendLine("                }");
            sb.AppendLine("                i++;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        return root;");
            sb.AppendLine("    }");
            sb.AppendLine("    void print_val(int val) { cout << val; }");
            sb.AppendLine("    void print_val(long long val) { cout << val; }");
            sb.AppendLine("    void print_val(double val) { cout << val; }");
            sb.AppendLine("    void print_val(bool val) { cout << (val ? \"true\" : \"false\"); }");
            sb.AppendLine("    void print_val(const string& val) { cout << \"\\\"\" << val << \"\\\"\"; }");
            sb.AppendLine("    void print_val(char val) { cout << \"\\\"\" << val << \"\\\"\"; }");
            sb.AppendLine("    void print_val(ListNode* head) {");
            sb.AppendLine("        cout << \"[\";");
            sb.AppendLine("        ListNode* curr = head;");
            sb.AppendLine("        while (curr) {");
            sb.AppendLine("            cout << curr->val;");
            sb.AppendLine("            if (curr->next) cout << \",\";");
            sb.AppendLine("            curr = curr->next;");
            sb.AppendLine("        }");
            sb.AppendLine("        cout << \"]\";");
            sb.AppendLine("    }");
            sb.AppendLine("    void print_val(TreeNode* root) {");
            sb.AppendLine("        if (!root) {");
            sb.AppendLine("            cout << \"[]\";");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
            sb.AppendLine("        vector<string> vals;");
            sb.AppendLine("        queue<TreeNode*> q;");
            sb.AppendLine("        q.push(root);");
            sb.AppendLine("        while (!q.empty()) {");
            sb.AppendLine("            TreeNode* curr = q.front();");
            sb.AppendLine("            q.pop();");
            sb.AppendLine("            if (curr) {");
            sb.AppendLine("                vals.push_back(to_string(curr->val));");
            sb.AppendLine("                q.push(curr->left);");
            sb.AppendLine("                q.push(curr->right);");
            sb.AppendLine("            } else {");
            sb.AppendLine("                vals.push_back(\"null\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        while (!vals.empty() && vals.back() == \"null\") {");
            sb.AppendLine("            vals.pop_back();");
            sb.AppendLine("        }");
            sb.AppendLine("        cout << \"[\";");
            sb.AppendLine("        for (size_t i = 0; i < vals.size(); ++i) {");
            sb.AppendLine("            cout << vals[i];");
            sb.AppendLine("            if (i + 1 < vals.size()) cout << \",\";");
            sb.AppendLine("        }");
            sb.AppendLine("        cout << \"]\";");
            sb.AppendLine("    }");
            sb.AppendLine("    template<typename T>");
            sb.AppendLine("    void print_val(const vector<T>& v) {");
            sb.AppendLine("        cout << \"[\";");
            sb.AppendLine("        for (size_t i = 0; i < v.size(); ++i) {");
            sb.AppendLine("            print_val(v[i]);");
            sb.AppendLine("            if (i + 1 < v.size()) cout << \",\";");
            sb.AppendLine("        }");
            sb.AppendLine("        cout << \"]\";");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("int main() {");
            sb.AppendLine("    while (true) {");
            for (int i = 0; i < paramTypes.Count; i++) {
                sb.AppendLine($"        string line{i};");
                sb.AppendLine($"        if (!getline(cin, line{i})) return 0;");
            }
            sb.AppendLine();
            var callArgs = new List<string>();
            for (int i = 0; i < paramTypes.Count; i++) {
                string cppType = GetCppType(paramTypes[i]);
                string parser = GetCppParser(paramTypes[i], $"line{i}");
                sb.AppendLine($"        {cppType} arg{i} = {parser};");
                callArgs.Add($"arg{i}");
            }
            sb.AppendLine("        Solution sol;");
            if (returnType == "void") {
                sb.AppendLine($"        sol.{methodName}({string.Join(", ", callArgs)});");
                if (paramTypes.Count > 0) sb.AppendLine("        BitSmithRunner::print_val(arg0);");
            } else {
                sb.AppendLine($"        auto res = sol.{methodName}({string.Join(", ", callArgs)});");
                sb.AppendLine("        BitSmithRunner::print_val(res);");
            }
            sb.AppendLine("        cout << endl;");
            sb.AppendLine("    }");
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string WrapC(string userCode, string methodName, List<string> paramTypes, string returnType) {
            var sb = new StringBuilder();
            sb.AppendLine("#define _GNU_SOURCE");
            sb.AppendLine("#include <stdio.h>");
            sb.AppendLine("#include <stdlib.h>");
            sb.AppendLine("#include <string.h>");
            sb.AppendLine("#include <stdbool.h>");
            sb.AppendLine("#include <math.h>");
            sb.AppendLine("#include <limits.h>");
            sb.AppendLine("#include <ctype.h>");
            sb.AppendLine();
            sb.AppendLine("struct ListNode {");
            sb.AppendLine("    int val;");
            sb.AppendLine("    struct ListNode *next;");
            sb.AppendLine("};");
            sb.AppendLine();
            sb.AppendLine("struct TreeNode {");
            sb.AppendLine("    int val;");
            sb.AppendLine("    struct TreeNode *left;");
            sb.AppendLine("    struct TreeNode *right;");
            sb.AppendLine("};");
            sb.AppendLine();
            sb.AppendLine(userCode);
            sb.AppendLine();
            sb.AppendLine(@"char* trim(char* str) {
    while (*str == ' ' || *str == '\t' || *str == '\r' || *str == '\n') str++;
    if (*str == '\0') return str;
    char* end = str + strlen(str) - 1;
    while (end > str && (*end == ' ' || *end == '\t' || *end == '\r' || *end == '\n')) end--;
    *(end + 1) = '\0';
    return str;
}

int parse_int(char* s) { return atoi(trim(s)); }
long long parse_long(char* s) { return strtoll(trim(s), NULL, 10); }
double parse_double(char* s) { return atof(trim(s)); }
bool parse_bool(char* s) {
    char* t = trim(s);
    return strcmp(t, ""true"") == 0 || strcmp(t, ""1"") == 0;
}
char* parse_string(char* s) {
    s = trim(s);
    int len = strlen(s);
    if (len >= 2 && s[0] == '""' && s[len - 1] == '""') {
        s[len - 1] = '\0';
        return s + 1;
    }
    return s;
}
char parse_char(char* s) {
    char* val = parse_string(s);
    return strlen(val) == 0 ? ' ' : val[0];
}

int* parse_vector_int(const char* raw_s, int* out_size) {
    *out_size = 0;
    while (*raw_s == ' ' || *raw_s == '\t' || *raw_s == '\r' || *raw_s == '\n') raw_s++;
    if (*raw_s != '[') return NULL;
    raw_s++;
    int capacity = 10;
    int* arr = (int*)malloc(sizeof(int) * capacity);
    int count = 0;
    while (*raw_s && *raw_s != ']') {
        while (*raw_s == ' ' || *raw_s == '\t' || *raw_s == '\r' || *raw_s == '\n' || *raw_s == ',') raw_s++;
        if (*raw_s == ']') break;
        char* end;
        long val = strtol(raw_s, &end, 10);
        if (raw_s == end) break;
        if (count >= capacity) {
            capacity *= 2;
            arr = (int*)realloc(arr, sizeof(int) * capacity);
        }
        arr[count++] = (int)val;
        raw_s = end;
    }
    *out_size = count;
    return arr;
}

char** parse_vector_string(const char* raw_s, int* out_size) {
    *out_size = 0;
    while (*raw_s == ' ' || *raw_s == '\t' || *raw_s == '\r' || *raw_s == '\n') raw_s++;
    if (*raw_s != '[') return NULL;
    raw_s++;
    int capacity = 10;
    char** arr = (char**)malloc(sizeof(char*) * capacity);
    int count = 0;
    while (*raw_s && *raw_s != ']') {
        while (*raw_s == ' ' || *raw_s == '\t' || *raw_s == '\r' || *raw_s == '\n' || *raw_s == ',') raw_s++;
        if (*raw_s == ']') break;
        if (*raw_s == '""') {
            raw_s++;
            const char* start = raw_s;
            while (*raw_s && *raw_s != '""') {
                if (*raw_s == '\\' && *(raw_s + 1)) raw_s += 2;
                else raw_s++;
            }
            int len = raw_s - start;
            char* val = (char*)malloc(len + 1);
            strncpy(val, start, len);
            val[len] = '\0';
            if (*raw_s == '""') raw_s++;
            if (count >= capacity) {
                capacity *= 2;
                arr = (char**)realloc(arr, sizeof(char*) * capacity);
            }
            arr[count++] = val;
        } else {
            const char* start = raw_s;
            while (*raw_s && *raw_s != ',' && *raw_s != ']') raw_s++;
            int len = raw_s - start;
            char* val = (char*)malloc(len + 1);
            strncpy(val, start, len);
            val[len] = '\0';
            int t = len - 1;
            while (t >= 0 && (val[t] == ' ' || val[t] == '\t' || val[t] == '\r' || val[t] == '\n')) {
                val[t] = '\0';
                t--;
            }
            if (count >= capacity) {
                capacity *= 2;
                arr = (char**)realloc(arr, sizeof(char*) * capacity);
            }
            arr[count++] = val;
        }
    }
    *out_size = count;
    return arr;
}

int** parse_matrix_int(const char* raw_s, int* out_rows, int** out_cols) {
    *out_rows = 0;
    *out_cols = NULL;
    while (*raw_s == ' ' || *raw_s == '\t' || *raw_s == '\r' || *raw_s == '\n') raw_s++;
    if (*raw_s != '[') return NULL;
    raw_s++;
    int row_cap = 10;
    int** matrix = (int**)malloc(sizeof(int*) * row_cap);
    int* col_sizes = (int*)malloc(sizeof(int) * row_cap);
    int row_count = 0;
    while (*raw_s && *raw_s != ']') {
        while (*raw_s == ' ' || *raw_s == '\t' || *raw_s == '\r' || *raw_s == '\n' || *raw_s == ',') raw_s++;
        if (*raw_s == ']') break;
        if (*raw_s == '[') {
            const char* start = raw_s;
            int open_brackets = 0;
            do {
                if (*raw_s == '[') open_brackets++;
                else if (*raw_s == ']') open_brackets--;
                raw_s++;
            } while (*raw_s && open_brackets > 0);
            int len = raw_s - start;
            char* sub = (char*)malloc(len + 1);
            strncpy(sub, start, len);
            sub[len] = '\0';
            int col_size = 0;
            int* row_vals = parse_vector_int(sub, &col_size);
            free(sub);
            if (row_count >= row_cap) {
                row_cap *= 2;
                matrix = (int**)realloc(matrix, sizeof(int*) * row_cap);
                col_sizes = (int*)realloc(col_sizes, sizeof(int) * row_cap);
            }
            matrix[row_count] = row_vals;
            col_sizes[row_count] = col_size;
            row_count++;
        } else {
            raw_s++;
        }
    }
    *out_rows = row_count;
    *out_cols = col_sizes;
    return matrix;
}

struct ListNode* parse_list_node(const char* raw_s) {
    int size = 0;
    int* vals = parse_vector_int(raw_s, &size);
    if (size == 0) {
        if (vals) free(vals);
        return NULL;
    }
    struct ListNode* head = (struct ListNode*)malloc(sizeof(struct ListNode));
    head->val = vals[0];
    head->next = NULL;
    struct ListNode* curr = head;
    for (int i = 1; i < size; i++) {
        curr->next = (struct ListNode*)malloc(sizeof(struct ListNode));
        curr = curr->next;
        curr->val = vals[i];
        curr->next = NULL;
    }
    free(vals);
    return head;
}

struct TreeNode* parse_tree_node(const char* raw_s) {
    while (*raw_s == ' ' || *raw_s == '\t' || *raw_s == '\r' || *raw_s == '\n') raw_s++;
    if (*raw_s != '[') return NULL;
    raw_s++;
    int capacity = 10;
    char** tokens = (char**)malloc(sizeof(char*) * capacity);
    int count = 0;
    while (*raw_s && *raw_s != ']') {
        while (*raw_s == ' ' || *raw_s == '\t' || *raw_s == '\r' || *raw_s == '\n' || *raw_s == ',') raw_s++;
        if (*raw_s == ']') break;
        const char* start = raw_s;
        while (*raw_s && *raw_s != ',' && *raw_s != ']') raw_s++;
        int len = raw_s - start;
        char* token = (char*)malloc(len + 1);
        strncpy(token, start, len);
        token[len] = '\0';
        int t = len - 1;
        while (t >= 0 && (token[t] == ' ' || token[t] == '\t' || token[t] == '\r' || token[t] == '\n')) {
            token[t] = '\0';
            t--;
        }
        if (count >= capacity) {
            capacity *= 2;
            tokens = (char**)realloc(tokens, sizeof(char*) * capacity);
        }
        tokens[count++] = token;
    }
    if (count == 0 || strcmp(tokens[0], ""null"") == 0 || strlen(tokens[0]) == 0) {
        for (int i = 0; i < count; i++) free(tokens[i]);
        free(tokens);
        return NULL;
    }
    struct TreeNode* root = (struct TreeNode*)malloc(sizeof(struct TreeNode));
    root->val = atoi(tokens[0]);
    root->left = NULL;
    root->right = NULL;
    struct TreeNode** queue = (struct TreeNode**)malloc(sizeof(struct TreeNode*) * count);
    int head = 0, tail = 0;
    queue[tail++] = root;
    int i = 1;
    while (head < tail && i < count) {
        struct TreeNode* curr = queue[head++];
        if (i < count) {
            if (strcmp(tokens[i], ""null"") != 0 && strlen(tokens[i]) > 0) {
                curr->left = (struct TreeNode*)malloc(sizeof(struct TreeNode));
                curr->left->val = atoi(tokens[i]);
                curr->left->left = NULL;
                curr->left->right = NULL;
                queue[tail++] = curr->left;
            }
            i++;
        }
        if (i < count) {
            if (strcmp(tokens[i], ""null"") != 0 && strlen(tokens[i]) > 0) {
                curr->right = (struct TreeNode*)malloc(sizeof(struct TreeNode));
                curr->right->val = atoi(tokens[i]);
                curr->right->left = NULL;
                curr->right->right = NULL;
                queue[tail++] = curr->right;
            }
            i++;
        }
    }
    for (int j = 0; j < count; j++) free(tokens[j]);
    free(tokens);
    free(queue);
    return root;
}

void print_int(int val) { printf(""%d"", val); }
void print_double(double val) { printf(""%g"", val); }
void print_bool(bool val) { printf(""%s"", val ? ""true"" : ""false""); }
void print_string(const char* val) { printf(""%s"", val); }
void print_char(char val) { printf(""%c"", val); }

void print_vector_int(int* arr, int size) {
    printf(""["");
    for (int i = 0; i < size; i++) {
        printf(""%d"", arr[i]);
        if (i + 1 < size) printf("","");
    }
    printf(""]"");
}

void print_vector_string(char** arr, int size) {
    printf(""["");
    for (int i = 0; i < size; i++) {
        printf(""\""%s\"""", arr[i]);
        if (i + 1 < size) printf("","");
    }
    printf(""]"");
}

void print_matrix_int(int** matrix, int rows, int* col_sizes) {
    printf(""["");
    for (int i = 0; i < rows; i++) {
        print_vector_int(matrix[i], col_sizes[i]);
        if (i + 1 < rows) printf("","");
    }
    printf(""]"");
}

void print_list_node(struct ListNode* head) {
    printf(""["");
    struct ListNode* curr = head;
    while (curr) {
        printf(""%d"", curr->val);
        if (curr->next) printf("","");
        curr = curr->next;
    }
    printf(""]"");
}

void print_tree_node(struct TreeNode* root) {
    if (!root) {
        printf(""[]"");
        return;
    }
    int cap = 1024;
    struct TreeNode** queue = malloc(sizeof(struct TreeNode*) * cap);
    int head = 0, tail = 0;
    queue[tail++] = root;
    int val_cap = 1024;
    char** vals = malloc(sizeof(char*) * val_cap);
    int val_count = 0;
    while (head < tail) {
        struct TreeNode* curr = queue[head++];
        if (val_count >= val_cap) {
            val_cap *= 2;
            vals = realloc(vals, sizeof(char*) * val_cap);
        }
        if (curr) {
            char buf[32];
            sprintf(buf, ""%d"", curr->val);
            vals[val_count++] = strdup(buf);
            if (tail + 2 >= cap) {
                cap *= 2;
                queue = realloc(queue, sizeof(struct TreeNode*) * cap);
            }
            queue[tail++] = curr->left;
            queue[tail++] = curr->right;
        } else {
            vals[val_count++] = strdup(""null"");
        }
    }
    while (val_count > 0 && strcmp(vals[val_count - 1], ""null"") == 0) {
        free(vals[val_count - 1]);
        val_count--;
    }
    printf(""["");
    for (int i = 0; i < val_count; i++) {
        printf(""%s"", vals[i]);
        if (i + 1 < val_count) printf("","");
        free(vals[i]);
    }
    printf(""]"");
    free(vals);
    free(queue);
}");
            sb.AppendLine();
            sb.AppendLine("int main() {");
            sb.AppendLine("    while (1) {");
            for (int i = 0; i < paramTypes.Count; i++) {
                sb.AppendLine($"        char* line{i} = NULL;");
                sb.AppendLine($"        size_t len{i} = 0;");
            }
            for (int i = 0; i < paramTypes.Count; i++) {
                sb.AppendLine($"        if (getline(&line{i}, &len{i}, stdin) == -1) {{");
                for (int j = 0; j <= i; j++) {
                    sb.AppendLine($"            free(line{j});");
                }
                sb.AppendLine("            break;");
                sb.AppendLine("        }");
            }
            sb.AppendLine();
            var callArgs = new List<string>();
            for (int i = 0; i < paramTypes.Count; i++) {
                switch (paramTypes[i]) {
                    case "integer":
                        sb.AppendLine($"        int arg{i} = parse_int(line{i});");
                        callArgs.Add($"arg{i}");
                        break;
                    case "long":
                        sb.AppendLine($"        long long arg{i} = parse_long(line{i});");
                        callArgs.Add($"arg{i}");
                        break;
                    case "double":
                        sb.AppendLine($"        double arg{i} = parse_double(line{i});");
                        callArgs.Add($"arg{i}");
                        break;
                    case "float":
                        sb.AppendLine($"        float arg{i} = (float)parse_double(line{i});");
                        callArgs.Add($"arg{i}");
                        break;
                    case "boolean":
                        sb.AppendLine($"        bool arg{i} = parse_bool(line{i});");
                        callArgs.Add($"arg{i}");
                        break;
                    case "string":
                        sb.AppendLine($"        char* arg{i} = parse_string(line{i});");
                        callArgs.Add($"arg{i}");
                        break;
                    case "character":
                        sb.AppendLine($"        char arg{i} = parse_char(line{i});");
                        callArgs.Add($"arg{i}");
                        break;
                    case "integer[]":
                        sb.AppendLine($"        int arg{i}Size = 0;");
                        sb.AppendLine($"        int* arg{i} = parse_vector_int(line{i}, &arg{i}Size);");
                        callArgs.Add($"arg{i}");
                        callArgs.Add($"arg{i}Size");
                        break;
                    case "string[]":
                        sb.AppendLine($"        int arg{i}Size = 0;");
                        sb.AppendLine($"        char** arg{i} = parse_vector_string(line{i}, &arg{i}Size);");
                        callArgs.Add($"arg{i}");
                        callArgs.Add($"arg{i}Size");
                        break;
                    case "integer[][]":
                        sb.AppendLine($"        int arg{i}Size = 0;");
                        sb.AppendLine($"        int* arg{i}ColSize = NULL;");
                        sb.AppendLine($"        int** arg{i} = parse_matrix_int(line{i}, &arg{i}Size, &arg{i}ColSize);");
                        callArgs.Add($"arg{i}");
                        callArgs.Add($"arg{i}Size");
                        callArgs.Add($"arg{i}ColSize");
                        break;
                    case "ListNode":
                        sb.AppendLine($"        struct ListNode* arg{i} = parse_list_node(line{i});");
                        callArgs.Add($"arg{i}");
                        break;
                    case "TreeNode":
                        sb.AppendLine($"        struct TreeNode* arg{i} = parse_tree_node(line{i});");
                        callArgs.Add($"arg{i}");
                        break;
                    default:
                        sb.AppendLine($"        int arg{i}Size = 0;");
                        sb.AppendLine($"        int* arg{i} = parse_vector_int(line{i}, &arg{i}Size);");
                        callArgs.Add($"arg{i}");
                        callArgs.Add($"arg{i}Size");
                        break;
                }
            }
            string cReturnType;
            switch (returnType) {
                case "integer": cReturnType = "int"; break;
                case "long": cReturnType = "long long"; break;
                case "double": cReturnType = "double"; break;
                case "float": cReturnType = "float"; break;
                case "boolean": cReturnType = "bool"; break;
                case "string": cReturnType = "char*"; break;
                case "character": cReturnType = "char"; break;
                case "integer[]": cReturnType = "int*"; break;
                case "string[]": cReturnType = "char**"; break;
                case "integer[][]": cReturnType = "int**"; break;
                case "ListNode": cReturnType = "struct ListNode*"; break;
                case "TreeNode": cReturnType = "struct TreeNode*"; break;
                default: cReturnType = "int"; break;
            }
            bool isArrayReturn = returnType == "integer[]" || returnType == "string[]" || returnType == "integer[][]";
            if (isArrayReturn) {
                sb.AppendLine("        int returnSize = 0;");
                if (returnType == "integer[][]") {
                    sb.AppendLine("        int* returnColumnSizes = NULL;");
                    callArgs.Add("&returnSize");
                    callArgs.Add("&returnColumnSizes");
                } else {
                    callArgs.Add("&returnSize");
                }
            }
            if (returnType == "void") {
                sb.AppendLine($"        {methodName}({string.Join(", ", callArgs)});");
                if (paramTypes.Count > 0) {
                    switch (paramTypes[0]) {
                        case "integer": sb.AppendLine("        print_int(arg0);"); break;
                        case "double":
                        case "float": sb.AppendLine("        print_double(arg0);"); break;
                        case "boolean": sb.AppendLine("        print_bool(arg0);"); break;
                        case "string": sb.AppendLine("        print_string(arg0);"); break;
                        case "character": sb.AppendLine("        print_char(arg0);"); break;
                        case "integer[]": sb.AppendLine("        print_vector_int(arg0, arg0Size);"); break;
                        case "string[]": sb.AppendLine("        print_vector_string(arg0, arg0Size);"); break;
                        case "integer[][]": sb.AppendLine("        print_matrix_int(arg0, arg0Size, arg0ColSize);"); break;
                        case "ListNode": sb.AppendLine("        print_list_node(arg0);"); break;
                        case "TreeNode": sb.AppendLine("        print_tree_node(arg0);"); break;
                        default: sb.AppendLine("        print_int(arg0);"); break;
                    }
                }
            } else {
                sb.AppendLine($"        {cReturnType} res = {methodName}({string.Join(", ", callArgs)});");
                switch (returnType) {
                    case "integer": sb.AppendLine("        print_int(res);"); break;
                    case "double":
                    case "float": sb.AppendLine("        print_double(res);"); break;
                    case "boolean": sb.AppendLine("        print_bool(res);"); break;
                    case "string": sb.AppendLine("        print_string(res);"); break;
                    case "character": sb.AppendLine("        print_char(res);"); break;
                    case "integer[]": sb.AppendLine("        print_vector_int(res, returnSize);"); break;
                    case "string[]": sb.AppendLine("        print_vector_string(res, returnSize);"); break;
                    case "integer[][]": sb.AppendLine("        print_matrix_int(res, returnSize, returnColumnSizes);"); break;
                    case "ListNode": sb.AppendLine("        print_list_node(res);"); break;
                    case "TreeNode": sb.AppendLine("        print_tree_node(res);"); break;
                    default: sb.AppendLine("        print_int(res);"); break;
                }
            }
            sb.AppendLine("        printf(\"\\n\");");
            sb.AppendLine();
            for (int i = 0; i < paramTypes.Count; i++) {
                sb.AppendLine($"        free(line{i});");
            }
            sb.AppendLine("    }");
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string WrapPython(string userCode, string methodName, List<string> paramTypes, string returnType) {
            var sb = new StringBuilder();
            sb.AppendLine("from typing import List, Optional");
            sb.AppendLine();
            sb.AppendLine("class ListNode:");
            sb.AppendLine("    def __init__(self, val=0, next=None):");
            sb.AppendLine("        self.val = val");
            sb.AppendLine("        self.next = next");
            sb.AppendLine();
            sb.AppendLine("class TreeNode:");
            sb.AppendLine("    def __init__(self, val=0, left=None, right=None):");
            sb.AppendLine("        self.val = val");
            sb.AppendLine("        self.left = left");
            sb.AppendLine("        self.right = right");
            sb.AppendLine();
            sb.AppendLine(userCode);
            sb.AppendLine();
            sb.AppendLine("import sys");
            sb.AppendLine("import json");
            sb.AppendLine();
            sb.AppendLine("def parse_list_node(arr):");
            sb.AppendLine("    if not arr: return None");
            sb.AppendLine("    dummy = ListNode(0)");
            sb.AppendLine("    curr = dummy");
            sb.AppendLine("    for v in arr:");
            sb.AppendLine("        curr.next = ListNode(v)");
            sb.AppendLine("        curr = curr.next");
            sb.AppendLine("    return dummy.next");
            sb.AppendLine();
            sb.AppendLine("def parse_tree_node(arr):");
            sb.AppendLine("    if not arr: return None");
            sb.AppendLine("    if arr[0] is None: return None");
            sb.AppendLine("    root = TreeNode(arr[0])");
            sb.AppendLine("    from collections import deque");
            sb.AppendLine("    queue = deque([root])");
            sb.AppendLine("    i = 1");
            sb.AppendLine("    while queue and i < len(arr):");
            sb.AppendLine("        curr = queue.popleft()");
            sb.AppendLine("        if i < len(arr):");
            sb.AppendLine("            if arr[i] is not None:");
            sb.AppendLine("                curr.left = TreeNode(arr[i])");
            sb.AppendLine("                queue.append(curr.left)");
            sb.AppendLine("            i += 1");
            sb.AppendLine("        if i < len(arr):");
            sb.AppendLine("            if arr[i] is not None:");
            sb.AppendLine("                curr.right = TreeNode(arr[i])");
            sb.AppendLine("                queue.append(curr.right)");
            sb.AppendLine("            i += 1");
            sb.AppendLine("    return root");
            sb.AppendLine();
            sb.AppendLine("def serialize_list_node(head):");
            sb.AppendLine("    res = []");
            sb.AppendLine("    curr = head");
            sb.AppendLine("    while curr:");
            sb.AppendLine("        res.append(curr.val)");
            sb.AppendLine("        curr = curr.next");
            sb.AppendLine("    return res");
            sb.AppendLine();
            sb.AppendLine("def serialize_tree_node(root):");
            sb.AppendLine("    if not root: return []");
            sb.AppendLine("    res = []");
            sb.AppendLine("    from collections import deque");
            sb.AppendLine("    queue = deque([root])");
            sb.AppendLine("    while queue:");
            sb.AppendLine("        curr = queue.popleft()");
            sb.AppendLine("        if curr:");
            sb.AppendLine("            res.append(curr.val)");
            sb.AppendLine("            queue.append(curr.left)");
            sb.AppendLine("            queue.append(curr.right)");
            sb.AppendLine("        else:");
            sb.AppendLine("            res.append(None)");
            sb.AppendLine("    while res and res[-1] is None:");
            sb.AppendLine("        res.pop()");
            sb.AppendLine("    return res");
            sb.AppendLine();
            sb.AppendLine("if __name__ == '__main__':");
            sb.AppendLine("    try:");
            sb.AppendLine("        lines = [line.strip() for line in sys.stdin if line.strip()]");
            sb.AppendLine($"        P = {paramTypes.Count}");
            sb.AppendLine("        idx = 0");
            sb.AppendLine("        while idx < len(lines):");
            sb.AppendLine("            tc_lines = lines[idx : idx + P]");
            sb.AppendLine("            if len(tc_lines) < P:");
            sb.AppendLine("                break");
            sb.AppendLine("            idx += P");
            sb.AppendLine("            params = []");
            for (int i = 0; i < paramTypes.Count; i++) {
                sb.AppendLine($"            val = tc_lines[{i}]");
                if (paramTypes[i] == "string") {
                    sb.AppendLine($"            if val.startswith('\"') and val.endswith('\"'): val = json.loads(val)");
                    sb.AppendLine($"            params.append(val)");
                } else if (paramTypes[i] == "character") {
                    sb.AppendLine($"            if val.startswith('\"') or val.startswith(\"'\"): val = val[1:-1]");
                    sb.AppendLine($"            params.append(val)");
                } else if (paramTypes[i] == "ListNode") {
                    sb.AppendLine($"            params.append(parse_list_node(json.loads(val)))");
                } else if (paramTypes[i] == "TreeNode") {
                    sb.AppendLine($"            params.append(parse_tree_node(json.loads(val)))");
                } else {
                    sb.AppendLine($"            params.append(json.loads(val))");
                }
            }
            sb.AppendLine("            sol = Solution()");
            sb.AppendLine($"            method_name = '{methodName}'");
            sb.AppendLine("            if not hasattr(sol, method_name):");
            sb.AppendLine("                import re");
            sb.AppendLine("                snake = re.sub(r'(?<!^)(?=[A-Z])', '_', method_name).lower()");
            sb.AppendLine("                if hasattr(sol, snake): method_name = snake");
            sb.AppendLine("                else:");
            sb.AppendLine("                    attrs = [a for a in dir(sol) if not a.startswith('_')]");
            sb.AppendLine("                    if attrs: method_name = attrs[0]");
            sb.AppendLine("            func = getattr(sol, method_name)");
            if (returnType == "void") {
                sb.AppendLine("            func(*params)");
                if (paramTypes.Count > 0) {
                    if (paramTypes[0] == "ListNode") sb.AppendLine("            res = serialize_list_node(params[0])");
                    else if (paramTypes[0] == "TreeNode") sb.AppendLine("            res = serialize_tree_node(params[0])");
                    else sb.AppendLine("            res = params[0]");
                    sb.AppendLine("            if isinstance(res, str): print(res)");
                    sb.AppendLine("            else: print(json.dumps(res, separators=(',', ':')))");
                }
            } else {
                sb.AppendLine("            res = func(*params)");
                if (returnType == "ListNode") {
                    sb.AppendLine("            res = serialize_list_node(res)");
                } else if (returnType == "TreeNode") {
                    sb.AppendLine("            res = serialize_tree_node(res)");
                }
                sb.AppendLine("            if isinstance(res, str): print(res)");
                sb.AppendLine("            else: print(json.dumps(res, separators=(',', ':')))");
            }
            sb.AppendLine("    except Exception as e:");
            sb.AppendLine("        print('ERROR:', e, file=sys.stderr)");
            sb.AppendLine("        sys.exit(1)");
            return sb.ToString();
        }

        private static string GetCSharpType(string jsonType) {
            if (jsonType.EndsWith("[]")) {
                return GetCSharpType(jsonType.Substring(0, jsonType.Length - 2)) + "[]";
            }
            switch(jsonType) {
                case "integer": return "int";
                case "long": return "long";
                case "double": return "double";
                case "float": return "float";
                case "boolean": return "bool";
                case "character": return "char";
                case "string": return "string";
                case "ListNode": return "ListNode";
                case "TreeNode": return "TreeNode";
                default: return "int";
            }
        }

        private static string WrapCsharp(string userCode, string methodName, List<string> paramTypes, string returnType) {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine();
            sb.AppendLine("public class ListNode {");
            sb.AppendLine("    public int val;");
            sb.AppendLine("    public ListNode next;");
            sb.AppendLine("    public ListNode(int val=0, ListNode next=null) {");
            sb.AppendLine("        this.val = val;");
            sb.AppendLine("        this.next = next;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("public class TreeNode {");
            sb.AppendLine("    public int val;");
            sb.AppendLine("    public TreeNode left;");
            sb.AppendLine("    public TreeNode right;");
            sb.AppendLine("    public TreeNode(int val=0, TreeNode left=null, TreeNode right=null) {");
            sb.AppendLine("        this.val = val;");
            sb.AppendLine("        this.left = left;");
            sb.AppendLine("        this.right = right;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine(userCode);
            sb.AppendLine();
            sb.AppendLine("public class BitSmithRunnerMain {");
            sb.AppendLine("    private static ListNode ParseListNode(string s) {");
            sb.AppendLine("        if (string.IsNullOrEmpty(s) || s.Trim() == \"null\") return null;");
            sb.AppendLine("        var arr = JsonSerializer.Deserialize<List<int>>(s);");
            sb.AppendLine("        if (arr == null || arr.Count == 0) return null;");
            sb.AppendLine("        ListNode dummy = new ListNode(0);");
            sb.AppendLine("        ListNode curr = dummy;");
            sb.AppendLine("        foreach (var v in arr) {");
            sb.AppendLine("            curr.next = new ListNode(v);");
            sb.AppendLine("            curr = curr.next;");
            sb.AppendLine("        }");
            sb.AppendLine("        return dummy.next;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static TreeNode ParseTreeNode(string s) {");
            sb.AppendLine("        if (string.IsNullOrEmpty(s) || s.Trim() == \"null\") return null;");
            sb.AppendLine("        using (var doc = JsonDocument.Parse(s)) {");
            sb.AppendLine("            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;");
            sb.AppendLine("            var array = doc.RootElement;");
            sb.AppendLine("            if (array.GetArrayLength() == 0) return null;");
            sb.AppendLine("            var first = array[0];");
            sb.AppendLine("            if (first.ValueKind == JsonValueKind.Null) return null;");
            sb.AppendLine("            TreeNode root = new TreeNode(first.GetInt32());");
            sb.AppendLine("            Queue<TreeNode> q = new Queue<TreeNode>();");
            sb.AppendLine("            q.Enqueue(root);");
            sb.AppendLine("            int i = 1;");
            sb.AppendLine("            int len = array.GetArrayLength();");
            sb.AppendLine("            while (q.Count > 0 && i < len) {");
            sb.AppendLine("                TreeNode curr = q.Dequeue();");
            sb.AppendLine("                if (i < len) {");
            sb.AppendLine("                    var val = array[i];");
            sb.AppendLine("                    if (val.ValueKind != JsonValueKind.Null) {");
            sb.AppendLine("                        curr.left = new TreeNode(val.GetInt32());");
            sb.AppendLine("                        q.Enqueue(curr.left);");
            sb.AppendLine("                    }");
            sb.AppendLine("                    i++;");
            sb.AppendLine("                }");
            sb.AppendLine("                if (i < len) {");
            sb.AppendLine("                    var val = array[i];");
            sb.AppendLine("                    if (val.ValueKind != JsonValueKind.Null) {");
            sb.AppendLine("                        curr.right = new TreeNode(val.GetInt32());");
            sb.AppendLine("                        q.Enqueue(curr.right);");
            sb.AppendLine("                    }");
            sb.AppendLine("                    i++;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            return root;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static List<int> SerializeListNode(ListNode head) {");
            sb.AppendLine("        var res = new List<int>();");
            sb.AppendLine("        ListNode curr = head;");
            sb.AppendLine("        while (curr != null) {");
            sb.AppendLine("            res.Add(curr.val);");
            sb.AppendLine("            curr = curr.next;");
            sb.AppendLine("        }");
            sb.AppendLine("        return res;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static List<int?> SerializeTreeNode(TreeNode root) {");
            sb.AppendLine("        var res = new List<int?>();");
            sb.AppendLine("        if (root == null) return res;");
            sb.AppendLine("        Queue<TreeNode> q = new Queue<TreeNode>();");
            sb.AppendLine("        q.Enqueue(root);");
            sb.AppendLine("        while (q.Count > 0) {");
            sb.AppendLine("            TreeNode curr = q.Dequeue();");
            sb.AppendLine("            if (curr != null) {");
            sb.AppendLine("                res.Add(curr.val);");
            sb.AppendLine("                q.Enqueue(curr.left);");
            sb.AppendLine("                q.Enqueue(curr.right);");
            sb.AppendLine("            } else {");
            sb.AppendLine("                res.Add(null);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        while (res.Count > 0 && res[res.Count - 1] == null) {");
            sb.AppendLine("            res.RemoveAt(res.Count - 1);");
            sb.AppendLine("        }");
            sb.AppendLine("        return res;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static void Main() {");
            sb.AppendLine("        try {");
            sb.AppendLine("            while (true) {");
            for (int i = 0; i < paramTypes.Count; i++) {
                sb.AppendLine($"                string line{i} = Console.ReadLine();");
                sb.AppendLine($"                if (line{i} == null) return;");
            }
            var callArgs = new List<string>();
            for (int i = 0; i < paramTypes.Count; i++) {
                string csharpType = GetCSharpType(paramTypes[i]);
                string parser;
                if (paramTypes[i] == "ListNode") parser = $"ParseListNode(line{i})";
                else if (paramTypes[i] == "TreeNode") parser = $"ParseTreeNode(line{i})";
                else parser = $"JsonSerializer.Deserialize<{csharpType}>(line{i})";
                sb.AppendLine($"                var arg{i} = {parser};");
                callArgs.Add($"arg{i}");
            }
            string csharpMethodName = char.ToUpper(methodName[0]) + methodName.Substring(1);
            sb.AppendLine("                var sol = new Solution();");
            if (returnType == "void") {
                sb.AppendLine($"                sol.{csharpMethodName}({string.Join(", ", callArgs)});");
                if (paramTypes.Count > 0) {
                    string serializeCall = "arg0";
                    if (paramTypes[0] == "ListNode") serializeCall = "SerializeListNode(arg0)";
                    else if (paramTypes[0] == "TreeNode") serializeCall = "SerializeTreeNode(arg0)";
                    sb.AppendLine($"                Console.WriteLine(JsonSerializer.Serialize({serializeCall}));");
                }
            } else {
                sb.AppendLine($"                var res = sol.{csharpMethodName}({string.Join(", ", callArgs)});");
                string serializeCall = "res";
                if (returnType == "ListNode") {
                    serializeCall = "SerializeListNode(res)";
                } else if (returnType == "TreeNode") {
                    serializeCall = "SerializeTreeNode(res)";
                }
                sb.AppendLine($"                Console.WriteLine(JsonSerializer.Serialize({serializeCall}));");
            }
            sb.AppendLine("            }");
            sb.AppendLine("        } catch (Exception ex) {");
            sb.AppendLine("            Console.Error.WriteLine(ex.Message);");
            sb.AppendLine("            Environment.Exit(1);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GetJavaType(string jsonType) {
            if (jsonType.EndsWith("[]")) return GetJavaType(jsonType.Substring(0, jsonType.Length - 2)) + "[]";
            switch(jsonType) {
                case "integer": return "int";
                case "long": return "long";
                case "double": return "double";
                case "float": return "float";
                case "boolean": return "boolean";
                case "character": return "char";
                case "string": return "String";
                case "ListNode": return "ListNode";
                case "TreeNode": return "TreeNode";
                default: return "int";
            }
        }

        private static string GetJavaParser(string jsonType, string varLine) {
            if (jsonType == "ListNode") return $"parseListNode({varLine})";
            if (jsonType == "TreeNode") return $"parseTreeNode({varLine})";
            if (jsonType == "integer") return $"Integer.parseInt({varLine}.trim())";
            if (jsonType == "long") return $"Long.parseLong({varLine}.trim())";
            if (jsonType == "double") return $"Double.parseDouble({varLine}.trim())";
            if (jsonType == "float") return $"Float.parseFloat({varLine}.trim())";
            if (jsonType == "boolean") return $"Boolean.parseBoolean({varLine}.trim())";
            if (jsonType == "character") return $"{varLine}.trim().replace(\"\\\"\", \"\").charAt(0)";
            if (jsonType == "string") return $"{varLine}.trim().startsWith(\"\\\"\") ? {varLine}.trim().substring(1, {varLine}.trim().length() - 1) : {varLine}.trim()";
            
            if (jsonType == "integer[]") return $"Arrays.stream(splitJsonArray({varLine})).mapToInt(Integer::parseInt).toArray()";
            if (jsonType == "long[]") return $"Arrays.stream(splitJsonArray({varLine})).mapToLong(Long::parseLong).toArray()";
            if (jsonType == "double[]") return $"Arrays.stream(splitJsonArray({varLine})).mapToDouble(Double::parseDouble).toArray()";
            if (jsonType == "string[]") return $"Arrays.stream(splitJsonArray({varLine})).map(s -> s.startsWith(\"\\\"\") ? s.substring(1, s.length() - 1) : s).toArray(String[]::new)";
            
            if (jsonType == "integer[][]") return $"Arrays.stream(splitJsonArray({varLine})).map(r -> Arrays.stream(splitJsonArray(r)).mapToInt(Integer::parseInt).toArray()).toArray(int[][]::new)";
            if (jsonType == "long[][]") return $"Arrays.stream(splitJsonArray({varLine})).map(r -> Arrays.stream(splitJsonArray(r)).mapToLong(Long::parseLong).toArray()).toArray(long[][]::new)";
            if (jsonType == "double[][]") return $"Arrays.stream(splitJsonArray({varLine})).map(r -> Arrays.stream(splitJsonArray(r)).mapToDouble(Double::parseDouble).toArray()).toArray(double[][]::new)";
            if (jsonType == "string[][]") return $"Arrays.stream(splitJsonArray({varLine})).map(r -> Arrays.stream(splitJsonArray(r)).map(s -> s.startsWith(\"\\\"\") ? s.substring(1, s.length() - 1) : s).toArray(String[]::new)).toArray(String[][]::new)";

            return $"null";
        }

        private static string WrapJava(string userCode, string methodName, List<string> paramTypes, string returnType) {
            // Strip public class Solution -> class Solution
            var cleanedCode = userCode.Replace("public class Solution", "class Solution");

            var sb = new StringBuilder();
            sb.AppendLine("import java.io.*;");
            sb.AppendLine("import java.util.*;");
            sb.AppendLine();
            sb.AppendLine("class ListNode {");
            sb.AppendLine("    int val;");
            sb.AppendLine("    ListNode next;");
            sb.AppendLine("    ListNode() {}");
            sb.AppendLine("    ListNode(int val) { this.val = val; }");
            sb.AppendLine("    ListNode(int val, ListNode next) { this.val = val; this.next = next; }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("class TreeNode {");
            sb.AppendLine("    int val;");
            sb.AppendLine("    TreeNode left;");
            sb.AppendLine("    TreeNode right;");
            sb.AppendLine("    TreeNode() {}");
            sb.AppendLine("    TreeNode(int val) { this.val = val; }");
            sb.AppendLine("    TreeNode(int val, TreeNode left, TreeNode right) {");
            sb.AppendLine("        this.val = val;");
            sb.AppendLine("        this.left = left;");
            sb.AppendLine("        this.right = right;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine(cleanedCode);
            sb.AppendLine();
            sb.AppendLine("public class SolutionRunner {");
            
            // ListNode parser
            sb.AppendLine("    private static ListNode parseListNode(String s) {");
            sb.AppendLine("        s = s.trim();");
            sb.AppendLine("        if (s.isEmpty() || s.equals(\"null\") || s.equals(\"[]\")) return null;");
            sb.AppendLine("        s = s.substring(1, s.length() - 1);");
            sb.AppendLine("        String[] parts = s.split(\",\");");
            sb.AppendLine("        ListNode dummy = new ListNode(0);");
            sb.AppendLine("        ListNode curr = dummy;");
            sb.AppendLine("        for (String p : parts) {");
            sb.AppendLine("            curr.next = new ListNode(Integer.parseInt(p.trim()));");
            sb.AppendLine("            curr = curr.next;");
            sb.AppendLine("        }");
            sb.AppendLine("        return dummy.next;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // TreeNode parser
            sb.AppendLine("    private static TreeNode parseTreeNode(String s) {");
            sb.AppendLine("        s = s.trim();");
            sb.AppendLine("        if (s.isEmpty() || s.equals(\"null\") || s.equals(\"[]\")) return null;");
            sb.AppendLine("        s = s.substring(1, s.length() - 1);");
            sb.AppendLine("        String[] parts = s.split(\",\");");
            sb.AppendLine("        if (parts.length == 0 || parts[0].trim().equals(\"null\") || parts[0].trim().isEmpty()) return null;");
            sb.AppendLine("        TreeNode root = new TreeNode(Integer.parseInt(parts[0].trim()));");
            sb.AppendLine("        Queue<TreeNode> q = new LinkedList<>();");
            sb.AppendLine("        q.add(root);");
            sb.AppendLine("        int i = 1;");
            sb.AppendLine("        while (!q.isEmpty() && i < parts.length) {");
            sb.AppendLine("            TreeNode curr = q.poll();");
            sb.AppendLine("            if (i < parts.length) {");
            sb.AppendLine("                String val = parts[i].trim();");
            sb.AppendLine("                if (!val.equals(\"null\") && !val.isEmpty()) {");
            sb.AppendLine("                    curr.left = new TreeNode(Integer.parseInt(val));");
            sb.AppendLine("                    q.add(curr.left);");
            sb.AppendLine("                }");
            sb.AppendLine("                i++;");
            sb.AppendLine("            }");
            sb.AppendLine("            if (i < parts.length) {");
            sb.AppendLine("                String val = parts[i].trim();");
            sb.AppendLine("                if (!val.equals(\"null\") && !val.isEmpty()) {");
            sb.AppendLine("                    curr.right = new TreeNode(Integer.parseInt(val));");
            sb.AppendLine("                    q.add(curr.right);");
            sb.AppendLine("                }");
            sb.AppendLine("                i++;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        return root;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // ListNode serializer
            sb.AppendLine("    private static String serializeListNode(ListNode head) {");
            sb.AppendLine("        StringBuilder sb = new StringBuilder();");
            sb.AppendLine("        sb.append(\"[\");");
            sb.AppendLine("        ListNode curr = head;");
            sb.AppendLine("        while (curr != null) {");
            sb.AppendLine("            sb.append(curr.val);");
            sb.AppendLine("            if (curr.next != null) sb.append(\",\");");
            sb.AppendLine("            curr = curr.next;");
            sb.AppendLine("        }");
            sb.AppendLine("        sb.append(\"]\");");
            sb.AppendLine("        return sb.toString();");
            sb.AppendLine("    }");
            sb.AppendLine();

            // TreeNode serializer
            sb.AppendLine("    private static String serializeTreeNode(TreeNode root) {");
            sb.AppendLine("        if (root == null) return \"[]\";");
            sb.AppendLine("        List<String> res = new ArrayList<>();");
            sb.AppendLine("        Queue<TreeNode> q = new LinkedList<>();");
            sb.AppendLine("        q.add(root);");
            sb.AppendLine("        while (!q.isEmpty()) {");
            sb.AppendLine("            TreeNode curr = q.poll();");
            sb.AppendLine("            if (curr != null) {");
            sb.AppendLine("                res.add(String.valueOf(curr.val));");
            sb.AppendLine("                q.add(curr.left);");
            sb.AppendLine("                q.add(curr.right);");
            sb.AppendLine("            } else {");
            sb.AppendLine("                res.add(\"null\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        while (!res.isEmpty() && res.get(res.size() - 1).equals(\"null\")) {");
            sb.AppendLine("            res.remove(res.size() - 1);");
            sb.AppendLine("        }");
            sb.AppendLine("        StringBuilder sb = new StringBuilder();");
            sb.AppendLine("        sb.append(\"[\");");
            sb.AppendLine("        for (int i = 0; i < res.size(); i++) {");
            sb.AppendLine("            sb.append(res.get(i));");
            sb.AppendLine("            if (i < res.size() - 1) sb.append(\",\");");
            sb.AppendLine("        }");
            sb.AppendLine("        sb.append(\"]\");");
            sb.AppendLine("        return sb.toString();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static String[] splitJsonArray(String s) {");
            sb.AppendLine("        s = s.trim();");
            sb.AppendLine("        if (s.length() < 2) return new String[0];");
            sb.AppendLine("        s = s.substring(1, s.length() - 1).trim();");
            sb.AppendLine("        if (s.isEmpty()) return new String[0];");
            sb.AppendLine("        java.util.List<String> list = new java.util.ArrayList<>();");
            sb.AppendLine("        int depth = 0;");
            sb.AppendLine("        StringBuilder curr = new StringBuilder();");
            sb.AppendLine("        boolean inQuotes = false;");
            sb.AppendLine("        for (int i = 0; i < s.length(); i++) {");
            sb.AppendLine("            char c = s.charAt(i);");
            sb.AppendLine("            if (c == '\"' && (i == 0 || s.charAt(i - 1) != '\\\\')) inQuotes = !inQuotes;");
            sb.AppendLine("            if (!inQuotes) {");
            sb.AppendLine("                if (c == '[') depth++;");
            sb.AppendLine("                else if (c == ']') depth--;");
            sb.AppendLine("                else if (c == ',' && depth == 0) {");
            sb.AppendLine("                    list.add(curr.toString().trim());");
            sb.AppendLine("                    curr = new StringBuilder();");
            sb.AppendLine("                    continue;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            curr.append(c);");
            sb.AppendLine("        }");
            sb.AppendLine("        list.add(curr.toString().trim());");
            sb.AppendLine("        return list.toArray(new String[0]);");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Java Main Execution Loop
            sb.AppendLine("    public static void main(String[] args) {");
            sb.AppendLine("        try {");
            sb.AppendLine("            BufferedReader reader = new BufferedReader(new InputStreamReader(System.in));");
            sb.AppendLine("            String line;");
            sb.AppendLine("            while ((line = reader.readLine()) != null) {");
            sb.AppendLine($"                int P = {paramTypes.Count};");
            sb.AppendLine("                String[] lines = new String[P];");
            sb.AppendLine("                lines[0] = line;");
            sb.AppendLine("                for (int i = 1; i < P; i++) {");
            sb.AppendLine("                    lines[i] = reader.readLine();");
            sb.AppendLine("                    if (lines[i] == null) return;");
            sb.AppendLine("                }");
            
            var callArgs = new List<string>();
            for (int i = 0; i < paramTypes.Count; i++) {
                string javaType = GetJavaType(paramTypes[i]);
                string parser = GetJavaParser(paramTypes[i], $"lines[{i}]");
                sb.AppendLine($"                {javaType} arg{i} = {parser};");
                callArgs.Add($"arg{i}");
            }
            sb.AppendLine("                Solution sol = new Solution();");
            
            if (returnType == "void") {
                sb.AppendLine($"                sol.{methodName}({string.Join(", ", callArgs)});");
                if (paramTypes.Count > 0) {
                    string serializedRes = "arg0";
                    if (paramTypes[0] == "ListNode") serializedRes = "serializeListNode(arg0)";
                    else if (paramTypes[0] == "TreeNode") serializedRes = "serializeTreeNode(arg0)";
                    else if (paramTypes[0].EndsWith("[]")) serializedRes = "Arrays.toString(arg0).replace(\" \", \"\")";
                    sb.AppendLine($"                System.out.println({serializedRes});");
                }
            } else {
                string serializedRes = "res";
                if (returnType == "ListNode") serializedRes = "serializeListNode(res)";
                else if (returnType == "TreeNode") serializedRes = "serializeTreeNode(res)";
                else if (returnType.EndsWith("[]")) serializedRes = "Arrays.toString(res).replace(\" \", \"\")";
                sb.AppendLine($"                var res = sol.{methodName}({string.Join(", ", callArgs)});");
                sb.AppendLine($"                System.out.println({serializedRes});");
            }
            sb.AppendLine("            }");
            sb.AppendLine("        } catch (Exception ex) {");
            sb.AppendLine("            System.err.println(ex.getMessage());");
            sb.AppendLine("            System.exit(1);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string NormalizeOutput(string output, bool allowAnyOrder) {
            output = output.Trim();
            if (output.StartsWith("[") && output.EndsWith("]")) {
                try {
                    var content = output.Substring(1, output.Length - 2);
                    if (string.IsNullOrWhiteSpace(content)) return "[]";
                    var parts = content.Split(',')
                        .Select(p => p.Trim())
                        .ToList();
                    
                    var normalizedParts = new List<string>();
                    bool allInts = true;
                    var parsedInts = new List<int>();
                    
                    foreach (var p in parts) {
                        if (int.TryParse(p, out int iVal)) {
                            parsedInts.Add(iVal);
                            normalizedParts.Add(iVal.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        } else {
                            allInts = false;
                            if (double.TryParse(p, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dVal)) {
                                normalizedParts.Add(dVal.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
                            } else {
                                normalizedParts.Add(p);
                            }
                        }
                    }

                    if (allowAnyOrder) {
                        if (allInts) {
                            parsedInts.Sort();
                            return "[" + string.Join(",", parsedInts) + "]";
                        } else {
                            normalizedParts = normalizedParts.OrderBy(x => {
                                if (double.TryParse(x, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val)) {
                                    return val;
                                }
                                return double.MaxValue;
                            }).ThenBy(x => x, StringComparer.Ordinal).ToList();
                        }
                    }
                    
                    return "[" + string.Join(",", normalizedParts) + "]";
                } catch { }
            }

            if (double.TryParse(output, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val)) {
                return val.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            }
            return output;
        }

        private static string NormalizeLanguage(string language) {
            if (string.IsNullOrWhiteSpace(language)) return string.Empty;
            var lang = language.Trim().ToLower();
            if (lang == "c#") return "csharp";
            return lang;
        }
    }

    public class SandboxResult {
        public string Status { get; set; } = string.Empty;
        public string? Stdout { get; set; }
        public string? Error { get; set; }
        public int? ExecutionTimeMs { get; set; }
    }
}
