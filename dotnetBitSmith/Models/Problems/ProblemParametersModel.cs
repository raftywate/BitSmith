using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Problems {
    public class ProblemParametersModel {
        const int MaxPageSize = 50; // Security: Prevent fetching 1 million rows
        private int _pageSize = 10;

        public int PageNumber { get; set; } = 1;

        public int PageSize {
            get => _pageSize;
            set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
        }
    }
}