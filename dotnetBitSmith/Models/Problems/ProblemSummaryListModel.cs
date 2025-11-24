using System;
using System.Collections.Generic;

namespace dotnetBitSmith.Models.Problems {
    public class ProblemSummaryListModel {
        public List<ProblemSummaryModel> Problems { get; set; } = new List<ProblemSummaryModel>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}