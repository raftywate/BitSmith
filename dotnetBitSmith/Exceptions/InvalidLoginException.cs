namespace dotnetBitSmith.Exceptions {
    /// Exception thrown for failed login attempts (invalid email or password).
    /// Results in an HTTP 401 Unauthorized.
    public class InvalidLoginException : Exception {
        public InvalidLoginException() { }

        public InvalidLoginException(string message) : base(message) { }

        public InvalidLoginException(string message, Exception inner) : base(message, inner) { }
    }
}
