using System.Security.Claims;
using dotnetBitSmith.Helpers;
using Microsoft.AspNetCore.Mvc;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Models.Users;
using Microsoft.AspNetCore.Authorization;

namespace dotnetBitSmith.Controllers {
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase {
        private readonly IUserService _userService;
        public UserController(IUserService userService) {
            _userService = userService;
        }

        [HttpGet("me")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(UserProfileModel), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserProfileModel>> GetMyProfile() {
            var userId = User.GetUserId();
            var profile = await _userService.GetMyProfileAsync(userId);
            return Ok(profile);
        }

        [HttpPut("me")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(UserProfileModel), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserProfileModel>> UpdateMyProfile([FromBody] UserProfileUpdateModel model) {
            var userId = User.GetUserId();
            var updatedProfile = await _userService.UpdateMyProfileAsync(userId, model);
            return Ok(updatedProfile);
        }

        [HttpGet("{userId}")] // Route: GET /api/user/{userId}
        [AllowAnonymous] // Overrides the [Authorize] on the class
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(UserProfileModel), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserProfileModel>> GetProfileById(Guid userId) {
            var profile = await _userService.GetProfileByIdAsync(userId);
            return Ok(profile);
        }
    }
}