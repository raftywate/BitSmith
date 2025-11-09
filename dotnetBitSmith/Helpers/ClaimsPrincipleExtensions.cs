using System.Security.Claims;

namespace dotnetBitSmith.Helpers {
    /// A static helper class to extend the ClaimsPrincipal (the 'User' object in a controller).
    public static class ClaimsPrincipalExtensions {
        /// Gets the User ID (as a Guid) from the JWT token's 'sub' claim.
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            // The "User" object on ControllerBase is auto-filled from their JWT.
            // We get the "sub" (Subject) claim, which we set to be the User's ID.
            var userIdString = user.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (string.IsNullOrEmpty(userIdString)) {
                // This should be impossible if [Authorize] is working,
                // but it's a critical safety check.
                throw new InvalidOperationException("User ID (sub claim) not found in token.");
            }

            if (Guid.TryParse(userIdString, out var userId)) {
                return userId;
            }
            
            // This should also be impossible if the token was issued by us.
            throw new InvalidOperationException("User ID in token is not a valid Guid.");
        }
    }
}