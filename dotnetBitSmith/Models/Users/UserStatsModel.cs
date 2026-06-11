namespace dotnetBitSmith.Models.Users {
    public class UserStatsModel {
        public int TotalSolved { get; set; }
        public int EasySolved { get; set; }
        public int MediumSolved { get; set; }
        public int HardSolved { get; set; }
        public int CurrentStreak { get; set; }
        public List<UserActivityDayModel> Activity { get; set; } = new();
        public List<AcceptedProblemModel> AcceptedProblems { get; set; } = new();
    }
}
