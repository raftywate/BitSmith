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
        private readonly IWebHostEnvironment _environment;

        public UserController(IUserService userService, IWebHostEnvironment environment) {
            _userService = userService;
            _environment = environment;
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

        [HttpPost("me/avatar")]
        [RequestSizeLimit(2_097_152)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(UserProfileModel), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserProfileModel>> UploadMyAvatar([FromForm] IFormFile avatar) {
            if (avatar == null || avatar.Length == 0) {
                return BadRequest("Choose an image file to upload.");
            }

            var allowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["image/jpeg"] = ".jpg",
                ["image/png"] = ".png",
                ["image/webp"] = ".webp",
                ["image/gif"] = ".gif"
            };

            if (!allowedTypes.TryGetValue(avatar.ContentType, out var extension)) {
                return BadRequest("Avatar must be a JPG, PNG, WebP, or GIF image.");
            }

            if (avatar.Length > 2_097_152) {
                return BadRequest("Avatar must be 2 MB or smaller.");
            }

            var userId = User.GetUserId();
            var uploadsDirectory = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads", "avatars");
            Directory.CreateDirectory(uploadsDirectory);

            var fileName = $"{userId:N}-{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(uploadsDirectory, fileName);

            await using (var stream = System.IO.File.Create(physicalPath)) {
                await avatar.CopyToAsync(stream);
            }

            var avatarUrl = $"{Request.Scheme}://{Request.Host}/uploads/avatars/{fileName}";
            var updatedProfile = await _userService.UpdateMyAvatarAsync(userId, avatarUrl);

            return Ok(updatedProfile);
        }

        [HttpDelete("me/avatar")]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(UserProfileModel), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserProfileModel>> RemoveMyAvatar() {
            var userId = User.GetUserId();
            var updatedProfile = await _userService.UpdateMyAvatarAsync(userId, null);
            return Ok(updatedProfile);
        }

        [HttpPut("me/preferences")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> UpdateMyPreferences([FromBody] UserPreferencesUpdateModel model) {
            var userId = User.GetUserId();
            await _userService.UpdateMyPreferencesAsync(userId, model.PreferredLanguage, model.LayoutState);
            return Ok();
        }

        [HttpPut("me/settings")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(UserProfileModel), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserProfileModel>> UpdateMySettings([FromBody] UserSettingsUpdateModel model) {
            var userId = User.GetUserId();
            var updatedProfile = await _userService.UpdateMySettingsAsync(userId, model);
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
