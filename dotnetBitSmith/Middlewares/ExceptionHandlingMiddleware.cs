using System.Net;
using System.Text.Json;
using dotnetBitSmith.Exceptions;

namespace dotnetBitSmith.Middleware {
    /// Global exception handling middleware to catch custom exceptions and
    /// return standardized, friendly JSON error responses.
    // --- The Class ---
    public class ExceptionHandlingMiddleware {
        // This is a "pointer" to the *next* station in the assembly line.
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        // The constructor where ASP.NET "injects" the next station.
        // This is how the pipeline is linked together.
        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger) {
            _next = next;
            _logger = logger;
        }

        // This is the main method that every piece of middleware MUST have.
        // The HTTP request and response are bundled in "httpContext".
        public async Task InvokeAsync(HttpContext httpContext) {
            try {
                // This is the "safety net" part.
                // It says: "Go ahead and try to run the REST of the
                // assembly line (all the other middleware and the controller)."
                await _next(httpContext);
            } catch (Exception ex) {
                // If *anything* down the line throws an error (e.g., our
                // AuthService throws DuplicateUserException), this "catch"
                // block will grab it. The app *will not crash*.
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        // This is our private helper to handle the error we just caught.
        private async Task HandleExceptionAsync(HttpContext context, Exception exception) {
            // We set the default error values...
            HttpStatusCode statusCode = HttpStatusCode.InternalServerError; // 500
            string message = "An unexpected error occurred.";

            // ...then we check if it's one of our *custom* exceptions.
            // This is the "smart" part.
            switch (exception) {
                case DuplicateUserException ex:
                    // We *know* this isn't a 500 bug, it's a 409 user error.
                    statusCode = HttpStatusCode.Conflict; // 409
                    message = ex.Message;
                    break;
                case InvalidLoginException ex:
                    // We *know* this is a 401 login failure.
                    statusCode = HttpStatusCode.Unauthorized; // 401
                    message = ex.Message;
                    break;
            }

            // We log the error so we (the developer) can see it.
            if (statusCode == HttpStatusCode.InternalServerError) {
                _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);
            }
            else {
                _logger.LogWarning(exception, "A handled exception occurred: {Message}", exception.Message);
            }

            // This is the most important part for Angular.
            // We are NOT sending a crash page. We are building a
            // clean, simple JSON object with the error message.
            var response = new { error = message };
            var payload = JsonSerializer.Serialize(response);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            // We send this clean JSON error back to the frontend.
            await context.Response.WriteAsync(payload);
        }
    }

    // --- The Extension Method ---
    public static class ExceptionHandlingMiddlewareExtensions {
        // This just creates a "shortcut" for our Program.cs file.
        // It lets us write "app.UseExceptionHandlingMiddleware()"
        // instead of the ugly "app.UseMiddleware<ExceptionHandlingMiddleware>()".
        public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder builder) {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}