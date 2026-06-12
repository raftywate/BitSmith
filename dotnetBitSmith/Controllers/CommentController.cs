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
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

        [HttpPut("{commentId}")]
        [Authorize]
        [ProducesResponseType(typeof(CommentViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<CommentViewModel>> UpdateComment(Guid commentId, [FromBody] CommentUpdateModel model) {
            Guid userId = User.GetUserId();
            var updatedComment = await _commentService.UpdateCommentAsync(commentId, userId, model);
            return Ok(updatedComment);
        }

        [HttpDelete("{commentId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> DeleteComment(Guid commentId) {
            Guid userId = User.GetUserId();
            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("admin");
            await _commentService.DeleteCommentAsync(commentId, userId, isAdmin);
            return NoContent();
        }

    }
}