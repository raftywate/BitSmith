using System.Security.Claims;
using dotnetBitSmith.Helpers;
using Microsoft.AspNetCore.Mvc;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Comments;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;

namespace dotnetBitSmith.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class CommentController : ControllerBase {
        private readonly ICommentService _commentService;

        public CommentController(ICommentService commentService) {
            _commentService = commentService;
        }

        [HttpPost]
        [Authorize]
        [EnableRateLimiting("post-content-policy")] 
        [ProducesResponseType(typeof(CommentViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<CommentViewModel>> CreateComment([FromBody] CommentCreateModel model) {
            Guid userId = User.GetUserId();
            var newComment = await _commentService.CreateCommentAsync(model, userId);
            return Ok(newComment);
        }

        [HttpGet("solution/{solutionId}")]
        [ProducesResponseType(typeof(IEnumerable<CommentViewModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<CommentViewModel>>> GetCommentsForSolution(Guid solutionId, [FromQuery] CommentParametersModel parameters) {
            var results = await _commentService.GetCommentsForSolutionAsync(solutionId, parameters);
            return Ok(results);
        }

    }
}