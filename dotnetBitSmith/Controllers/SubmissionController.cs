using System.Security.Claims;
using dotnetBitSmith.Helpers;
using dotnetBitSmith.Services;
using Microsoft.AspNetCore.Mvc;
using dotnetBitSmith.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using dotnetBitSmith.Models.Submissions;
using Microsoft.AspNetCore.Authorization;

namespace dotnetBitSmith.Controllers {
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SubmissionController : ControllerBase {
        private readonly ISubmissionService _submissionService;
        public SubmissionController(ISubmissionService submissionService) {
            _submissionService = submissionService;
        }

        [HttpPost]
        [EnableRateLimiting("submit-policy")] // Apply our strict "submit" policy
        [ProducesResponseType(typeof(SubmissionResultModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult> CreateSubmission([FromBody] SubmissionCreateModel model) {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) {
                return Unauthorized();
            }

            var userId = Guid.Parse(userIdString);
            var newSubmission = await _submissionService.CreateSubmissionAsync(model, userId);
            return Ok(newSubmission);
        }

        [HttpPost("run-samples")]
        [EnableRateLimiting("submit-policy")]
        [ProducesResponseType(typeof(IEnumerable<SampleRunResultModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<IEnumerable<SampleRunResultModel>>> RunSampleTests([FromBody] SampleRunRequestModel model) {
            Guid userId = User.GetUserId();
            var results = await _submissionService.RunSampleTestsAsync(model, userId);
            return Ok(results);
        }

        [HttpPost("run-code")]
        [EnableRateLimiting("submit-policy")]
        [ProducesResponseType(typeof(RunCodeResultModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<RunCodeResultModel>> RunCode([FromBody] RunCodeRequestModel model) {
            Guid userId = User.GetUserId();
            var result = await _submissionService.RunCustomCodeAsync(model, userId);
            return Ok(result);
        }

        [HttpGet("problem/{problemId}")]
        [ProducesResponseType(typeof(IEnumerable<SubmissionDetailModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> GetSubmissionForProblem(Guid problemId) {
            Guid userId = User.GetUserId();

            var submissions = await _submissionService.GetMySubmissionsForProblemAsync(problemId, userId);
            return Ok(submissions);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(SubmissionDetailModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> GetSubmission(Guid id) {
            Guid userId = User.GetUserId();
            var submission = await _submissionService.GetSubmissionByIdAsync(id, userId);
            if (submission == null) {
                return NotFound("Submission not found.");
            }
            return Ok(submission);
        }
    }
}
