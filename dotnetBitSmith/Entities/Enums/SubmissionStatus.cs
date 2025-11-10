namespace dotnetBitSmith.Entities.Enums {
    public enum SubmissionStatus {
        Pending,
        Running,
        Accepted,
        WrongAnswer,
        RuntimeError,
        InternalError,
        CompilationError,
        TimeLimitExceeded,
        MemoryLimitExceeded
    }
}