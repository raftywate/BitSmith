using System.Security.Claims;
using dotnetBitSmith.Helpers;
using dotnetBitSmith.Services;
using Microsoft.AspNetCore.Mvc;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Solutions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;

namespace dotnetBitSmith.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class SolutionController : ControllerBase {
        private readonly ISolutionService _solutionService;
        public SolutionController(ISolutionService solutionService) {
            _solutionService = solutionService;
        }

        [HttpPost]
        [Authorize]
        [EnableRateLimiting("post-content-policy")] // Apply our strict "submit" policy
        [ProducesResponseType(typeof(SolutionSummaryModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult> CreateSolution(SolutionCreateModel model) {
            var userId = User.GetUserId();
            var newSolution = await _solutionService.CreateSolutionAsync(model, userId);
            return Ok(newSolution);
        }


        [HttpGet("problem/{problemId}")]
        [ProducesResponseType(typeof(IEnumerable<SolutionSummaryModel>), StatusCodes.Status200OK)]
        public async Task<ActionResult> GetSolutionForProblem(Guid problemId) {
            var solutions = await _solutionService.GetSolutionsForProblemAsync(problemId);
            return Ok(solutions);
        }
    }
}