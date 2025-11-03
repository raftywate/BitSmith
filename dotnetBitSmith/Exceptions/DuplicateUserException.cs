namespace dotnetBitSmith.Exceptions {
     /// Exception thrown when a user registration fails due to a duplicate username or email.
    /// Results in an HTTP 409 Conflict.
    public class DuplicateUserException : Exception {
        public DuplicateUserException() { }
        public DuplicateUserException(string message) : base(message) { }
        public DuplicateUserException(string message, Exception inner) : base(message, inner) { }
        
    }
}