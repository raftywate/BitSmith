using System;

namespace dotnetBitSmith.Exceptions {
    public class UserVerificationRequiredException : Exception {
        public string Email { get; }
        public UserVerificationRequiredException(string email, string message) : base(message) {
            Email = email;
        }
    }
}
