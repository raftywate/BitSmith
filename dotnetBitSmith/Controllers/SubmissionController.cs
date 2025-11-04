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
    [EnableRateLimiting("submit-policy")]
    public class SubmissionController : ControllerBase {
        private readonly ISubmissionService _submissionService;
        public SubmissionController(ISubmissionService submissionService) {
            _submissionService = submissionService;
        }

        [HttpPost]
        public async Task<ActionResult> CreateSubmission([FromBody] SubmissionCreateModel model) {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) {
                return Unauthorized();
            }

            var userId = Guid.Parse(userIdString);
            var newSubmission = await _submissionService.CreateSubmissionAsync(model, userId);
            return Ok(newSubmission);
        }
    }
}