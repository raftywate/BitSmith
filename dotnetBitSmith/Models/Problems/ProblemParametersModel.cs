using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Problems {
    public class ProblemParametersModel {
        const int MaxPageSize = 100;
        private int _pageSize = 50;

        public int PageNumber { get; set; } = 1;

        public int PageSize {
            get => _pageSize;
            set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
        }

        // Search by title (contains) or problem number (exact)
        public string? Search { get; set; }

        // Filter by one or more category IDs (comma-separated or repeated param)
        public List<Guid>? CategoryIds { get; set; }
    }
}