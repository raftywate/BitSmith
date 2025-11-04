using System.Security.Claims;
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

        [HttpGet("problem/{problemId}")]
        [ProducesResponseType(typeof(IEnumerable<SubmissionDetailModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> GetSubmissionForProblem(Guid problemId) {
            Guid userId = GetUserIdFromToken();

            var submissions = await _submissionService.GetMySubmissionsForProblemAsync(problemId, userId);
            return Ok(submissions);
        }

        private Guid GetUserIdFromToken() {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) {
                throw new InvalidOperationException("User ID not found in token. This should not happen.");
            }

            return Guid.Parse(userIdString);
        }
    }
}