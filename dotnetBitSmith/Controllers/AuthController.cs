using System;
using System.Threading.Tasks;
using dotnetBitSmith.Entities;
using Microsoft.AspNetCore.Mvc;
using dotnetBitSmith.Interfaces;
using dotnetBitSmith.Exceptions;
using dotnetBitSmith.Models.Auth;

namespace dotnetBitSmith.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService) {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<ActionResult> Register([FromBody] UserRegisterModel model) {
            var authResponse = await _authService.RegisterAsync(model);
            return Ok(authResponse);
        }
        
        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] UserLoginModel model) {
            var authResponse = await _authService.LoginAsync(model);
            return Ok(authResponse);
        }
    }
}
