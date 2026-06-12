using System.Security.Claims;
using dotnetBitSmith.Helpers;
using dotnetBitSmith.Services;
using Microsoft.AspNetCore.Mvc;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Solutions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

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

        [HttpGet("user/{username}")]
        [ProducesResponseType(typeof(IEnumerable<SolutionSummaryModel>), StatusCodes.Status200OK)]
        public async Task<ActionResult> GetSolutionsByUsername(string username, [FromServices] dotnetBitSmith.Data.ApplicationDbContext context) {
            var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
            if (user == null) return NotFound("User not found.");
            var solutions = await _solutionService.GetSolutionsByUserAsync(user.Id);
            return Ok(solutions);
        }

        [HttpGet("{solutionId}")]
        [ProducesResponseType(typeof(SolutionDetailModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> GetSolutionById(Guid solutionId) {
            var solution = await _solutionService.GetSolutionByIdAsync(solutionId);
            return Ok(solution);
        }

        [HttpPut("{solutionId}")]
        [Authorize]
        [ProducesResponseType(typeof(SolutionDetailModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> UpdateSolution(Guid solutionId, [FromBody] SolutionUpdateModel model) {
            Guid userId = User.GetUserId();
            var updatedSolution = await _solutionService.UpdateSolutionAsync(solutionId, userId, model);
            return Ok(updatedSolution);
        }

        [HttpDelete("{solutionId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> DeleteSolution(Guid solutionId) {
            Guid userId = User.GetUserId();
            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("admin");
            await _solutionService.DeleteSolutionAsync(solutionId, userId, isAdmin);
            return NoContent();
        }
    }
}
