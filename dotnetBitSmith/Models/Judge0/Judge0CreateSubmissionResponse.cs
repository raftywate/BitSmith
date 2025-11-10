using System.Text.Json.Serialization;

namespace dotnetBitSmith.Models.Judge0 {
    public class Judge0CreateSubmissionResponse {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}