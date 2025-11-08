using dotnetBitSmith.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace dotnetBitSmith.Models.Votes {
    public class VoteModel {
        [Required]
        public Guid EntityId { get; set; }

        [Required]
        public VotableEntityType EntityType { get; set; }

        [Required]
        public bool IsUpvote { get; set; }
    }
}