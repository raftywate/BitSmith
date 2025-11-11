using dotnetBitSmith.Data;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Entities.Enums;
using dotnetBitSmith.Models.Judge0;
using System.Text.Json;
using System.Text;

namespace dotnetBitSmith.Services {
    public class Judge0CompilationService : ICompilationService {
        private readonly string _judge0ApiUrl;
        private readonly string _judge0ApiKey;
        private readonly string _judge0ApiHost;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<Judge0CompilationService> _logger;

        public Judge0CompilationService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<Judge0CompilationService> logger) {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            
            _judge0ApiUrl = _configuration["Judge0Settings:ApiUrl"] ?? throw new InvalidOperationException("Judge0 API URL is not configured");
            _judge0ApiKey = _configuration["Judge0Settings:ApiKey"] ?? throw new InvalidOperationException("Judge0 API Key is not configured");

            //Extract the "host" from the URL for the RapidAPI header
            _judge0ApiHost = new Uri(_judge0ApiUrl).Host;
        }

        public async Task<Submission> JudgeSubmissionAsync(Submission submission) {
            _logger.LogInformation("Starting judgment for Submission {SubmissionId}", submission.Id);

            try {
                // Map our Language string (e.g., "csharp") to a Judge0 Language ID (e.g., 51)
                int languageId = GetLanguageId(submission.Language);

                // Creating the DTO to send to Judge0
                var createRequest = new Judge0CreateSubmissionRequest {
                    LanguageId = languageId,
                    SourceCode = submission.Code,
                    StandardInputs = null
                };

                // Creating the HttpClient
                var client = _httpClientFactory.CreateClient();
                // (Adding the default headers for RapidAPI: 'X-RapidAPI-Key' and 'X-RapidAPI-Host')

                client.DefaultRequestHeaders.Add("X-RapidAPI-Key", _judge0ApiKey);
                client.DefaultRequestHeaders.Add("X-RapidAPI-Host", _judge0ApiHost);

                // Serialize the request and POST it to Judge0
                var jsonRequest = JsonSerializer.Serialize(createRequest);
                //wrapping it in http object that holds our JSON by labelling it as "application/json", so Judge0 knows it's receiving json and not random text 
                var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                //sending our wrapped envelope to Judge0 server and awaiting the response
                var httpResponse = await client.PostAsync($"{_judge0ApiUrl}/submissions?base64_encoded=false&wait=false", httpContent);

                if (!httpResponse.IsSuccessStatusCode) {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Judge0 failed to create submission. Status: {StatusCode}, Body: {ErrorBody}", httpResponse.StatusCode, errorContent);
                    throw new InvalidOperationException("Failed to create submission with Judge0.");
                }

                //Judge0 server's resply in JSON
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                
                //Converting it back to C# object
                var createResponse = JsonSerializer.Deserialize<Judge0CreateSubmissionResponse>(responseContent);
                if (createResponse == null || string.IsNullOrEmpty(createResponse.Token)) {
                    throw new InvalidOperationException("Judge0 returned an invalid token.");
                }
                //it's the token the server returned that has the number for our answer that it will give.
                //we will use this token to ask for the submission result
                var judgeToken = createResponse.Token;

                _logger.LogInformation("Submission {SubmissionId} created on Judge0 with token {JudgeToken}", submission.Id, judgeToken);

                Judge0GetSubmissionResponse? finalJudgeResponse = null;
                int pollAttempts = 0;

                //trying to get the status like "Accepeted", "Wrong answer" for the earlier provided ticket number 
                while (true) {
                    pollAttempts++;
                    if (pollAttempts > 20) { //Failsafe: 20 attempts * 500ms = 10 seconds
                        throw new InvalidOperationException("Polling Judge0 timed out.");
                    }
                    // Wait 500ms before checking the status
                    await Task.Delay(500);

                    //,aking a new HTTP request for asking the status of our order i.e. the submission result
                    var getResponse = await client.GetAsync($"{_judge0ApiUrl}/submissions/{judgeToken}?base64_encoded=false");
                    if (!getResponse.IsSuccessStatusCode) {
                        throw new InvalidOperationException("Failed to poll for submission result from Judge0.");
                    }

                    //getting the response and then deserializing it
                    var getResponseContent = await getResponse.Content.ReadAsStringAsync();
                    finalJudgeResponse = JsonSerializer.Deserialize<Judge0GetSubmissionResponse>(getResponseContent);

                    if (finalJudgeResponse == null) {
                        throw new InvalidOperationException("Judge0 returned a null polling response.");
                    }

                    // Status 1 = "In Queue", 2 = "Processing"
                    if (finalJudgeResponse.Status.Id > 2) {
                        // The submission is finished (Accepted, Rejected, etc.)
                        _logger.LogInformation("Submission {SubmissionId} finished with status: {Status}", submission.Id, finalJudgeResponse.Status.Description);
                        break;
                    }
                    // If status is 1 or 2, the loop will just continue
                }

                //updating our submission entity
                MapJudge0StatusToSubmission(submission, finalJudgeResponse);

                _context.Submissions.Update(submission);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Submission {SubmissionId} updated in local DB with final status.", submission.Id);

                return submission;
            } catch (Exception ex) {
               _logger.LogError(ex, "Error during judgment for Submission {SubmissionId}", submission.Id);
                
                // If the judge fails, set our status to "Internal Error"
                // This is a "safety net" so the user isn't stuck on "Pending"
                submission.Status = SubmissionStatus.InternalError;
                _context.Submissions.Update(submission);
                await _context.SaveChangesAsync();
                return submission;
            } 
        }

        private int GetLanguageId(string language) {
            // This is a simple mapper. We can add more languages later.
            // You can find IDs on the Judge0 website.
            switch (language.ToLower()) {
                case "csharp":
                    return 51; // .NET Mono csc 6.12.0.0
                case "python":
                    return 71; // Python 3.8.1
                case "java":
                    return 62; // Java OpenJDK 13.0.1
                case "cpp":
                    return 54; // C++ GCC 9.2.0
                case "c":
                    return 50; // C GCC 9.2.0
                case "javascript":
                    return 63; // JavaScript Node.js 12.14.0
                default:
                    throw new NotSupportedException($"Language '{language}' is not supported.");
            }
        }

        private void MapJudge0StatusToSubmission(Submission submission, Judge0GetSubmissionResponse judgeResponse) {
            // Judge0 Status IDs:
            // 1: In Queue
            // 2: Processing
            // 3: Accepted
            // 4: Wrong Answer
            // 5: Time Limit Exceeded
            // 6: Compilation Error
            // 7-12: Runtime Errors
            // 13: Internal Error

            submission.Status = judgeResponse.Status.Id switch {
                3 => SubmissionStatus.Accepted,
                4 => SubmissionStatus.WrongAnswer,
                5 => SubmissionStatus.TimeLimitExceeded,
                6 => SubmissionStatus.CompilationError,
                _ => SubmissionStatus.RuntimeError // All other errors
            };

            // Parse time (e.g., "0.051s") to milliseconds
            if (double.TryParse(judgeResponse.Time?.Replace("s", ""), out double timeInSeconds)) {
                submission.ExecutionTimeMs = (int)(timeInSeconds * 1000);
            }

            // Memory is already in KB
            submission.ExecutionMemoryKb = judgeResponse.Memory;

            // We could also save the stdout/stderr, but we'll skip for now
        }
    }
}