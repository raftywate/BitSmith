using System;
/// Custom exception thrown when a specific resource (like a Problem or User)
/// is not found in the database.
/// This will be caught by the middleware and translated into a 404 Not Found.
namespace dotnetBitSmith.Exceptions {
    public class NotFoundException : ApplicationException {
        public NotFoundException(string message) : base(message) {}
        public NotFoundException(string entity, object key) : base("Entity " + entity + " with key " + key + "was not found.") {}
    }
}