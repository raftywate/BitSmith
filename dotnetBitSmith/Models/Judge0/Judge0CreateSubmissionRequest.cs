using System.Text.Json.Serialization;

namespace dotnetBitSmith.Models.Judge0 {
    public class Judge0CreateSubmissionRequest {
        [JsonPropertyName("language_id")]
        public int LanguageId { get; set; }

        [JsonPropertyName("source_code")]
        public string SourceCode { get; set; } = string.Empty;

        [JsonPropertyName("stdin")]
        public string? StandardInputs { get; set; }
    }
}