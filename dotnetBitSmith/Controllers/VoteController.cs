using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Votes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace dotnetBitSmith.Controllers {
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("post-content-policy")]
    public class VoteController : ControllerBase {
        private readonly IVoteService _voteService;
        public VoteController(IVoteService voteService) {
            _voteService = voteService;
        }

        [HttpPost]
        public async Task<ActionResult<int>> CastVote([FromBody] VoteModel voteModel) {
            Guid userId = GetUserIdFromToken();
            var newVoteCount = await _voteService.CastVoteAsync(voteModel, userId);
            return Ok(newVoteCount);
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