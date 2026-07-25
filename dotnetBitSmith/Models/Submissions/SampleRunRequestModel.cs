using System;
using System.Collections.Generic;

namespace dotnetBitSmith.Models.Submissions {
    public class SampleRunRequestModel {
        public Guid ProblemId { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public List<TestCaseDto>? TestCases { get; set; }
    }

    public class TestCaseDto {
        public Guid Id { get; set; }
        public string Input { get; set; } = string.Empty;
        public string ExpectedOutput { get; set; } = string.Empty;
    }
}
