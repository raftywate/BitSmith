using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Problems;
using Microsoft.AspNetCore.Authorization;

namespace dotnetBitSmith.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class ProblemController : ControllerBase {
        private readonly IProblemService _problemService; //We are using IProblemService instead of ProblemService to ensure there's no Dependency Inversion
        //The controller is a high-level module so, it should not be dependent on a low-level implementation like that of ProblemService as it violates the "D" in SOLID Principle
        public ProblemController(IProblemService problemService) {
            _problemService = problemService;
        }

        [HttpGet]    
        [ProducesResponseType(typeof(IEnumerable<ProblemSummaryModel>), StatusCodes.Status200OK)] //just for documentation in Swagger UI
        public async Task<ActionResult<IEnumerable<ProblemSummaryModel>>> GetProblemsAsync() {
            var problems = await _problemService.GetProblemsAsync();
            return Ok(problems);
        }

        [HttpGet("{problemId}", Name = "GetProblemById")]
        [ProducesResponseType(typeof(IEnumerable<ProblemDetailModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProblemDetailModel>> GetProblemByIdAsync(Guid problemId) {
            var problem = await _problemService.GetProblemByIdAsync(problemId);
            return Ok(problem);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ProblemDetailModel), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // For invalid Category IDs
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ProblemDetailModel>> CreateProblem([FromBody] ProblemCreateModel model) {
            // The [Authorize] attribute ensures the user is logged in and an Admin.
            // The "User" object on ControllerBase is auto-filled from their JWT.
            // We get the "sub" (Subject) claim, which we set to be the User's ID.
            var authorIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(authorIdString)) {
                return Unauthorized();
            }

            var authorId = Guid.Parse(authorIdString);
            System.Console.WriteLine("THERE!");
            var newProblem = await _problemService.CreateProblemAsync(model, authorId);
            System.Console.WriteLine("HERE!");

            return CreatedAtRoute("GetProblemById", new { problemId = newProblem.Id }, newProblem);
        }
    }
}