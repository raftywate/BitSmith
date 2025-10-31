using System;
using dotnetBitSmith.Services;
using Microsoft.AspNetCore.Mvc;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Problems;

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

        [HttpGet("problemId")]    
        [ProducesResponseType(typeof(IEnumerable<ProblemDetailModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProblemDetailModel>> GetProblemByIdAsync(Guid problemId) {
            var problem = await _problemService.GetProblemByIdAsync(problemId);
            return Ok(problem);
        }
    }
}