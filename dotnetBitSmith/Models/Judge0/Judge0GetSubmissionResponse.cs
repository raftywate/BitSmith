using System.Text.Json.Serialization;

namespace dotnetBitSmith.Models.Judge0 {
    public class Judge0GetSubmissionResponse {
        [JsonPropertyName("status")]
        public Judge0Status Status { get; set; } = new Judge0Status();

        [JsonPropertyName("stdout")]
        public string? StandardOutput { get; set; }

        [JsonPropertyName("stderr")]
        public string? StandardError { get; set; }

        [JsonPropertyName("compile_output")]
        public string? CompileOutput { get; set; }

        [JsonPropertyName("time")]
        public string? Time { get; set; }

        [JsonPropertyName("memory")]
        public int? Memory { get; set; }
    }
    
    public class Judge0Status {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }
}