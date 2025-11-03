using dotnetBitSmith.Entities;
using dotnetBitSmith.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace dotnetBitSmith.Data {
    public class ApplicationDbContext : DbContext {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        public DbSet<User> Users { get; set; }
        public DbSet<Vote> Votes { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Problem> Problems { get; set; }
        public DbSet<Solution> Solutions { get; set; }
        public DbSet<TestCase> TestCases { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<CodeSnippet> CodeSnippets { get; set; }
        public DbSet<ProblemCategory> ProblemCategories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            base.OnModelCreating(modelBuilder);

            // 1. Configure the Problem <-> Category many-to-many join table
            modelBuilder.Entity<ProblemCategory>()
                .HasKey(pc => new { pc.ProblemId, pc.CategoryId }); // Composite Key

            modelBuilder.Entity<ProblemCategory>()
                .HasOne(pc => pc.Problem)
                .WithMany(p => p.ProblemCategories)
                .HasForeignKey(pc => pc.ProblemId);

            modelBuilder.Entity<ProblemCategory>()
                .HasOne(pc => pc.Category)
                .WithMany(c => c.ProblemCategories)
                .HasForeignKey(pc => pc.CategoryId);

            // 2. Configure the self-referencing Comment replies
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.ClientSetNull); // Prevents cascade delete cycles

            modelBuilder.Entity<Problem>()
                .HasOne(p => p.Author)
                .WithMany(u => u.ProblemsAuthored)
                .HasForeignKey(p => p.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull); // Use ClientSetNull for nullable foreign key
            
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany(u => u.CommentsAuthored)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull); // Breaks the User->Solution->Comment cascade cycle

            // 3. Configure Enums to be stored as strings (e.g., "Easy", "Pending")
            modelBuilder.Entity<Problem>()
                .Property(p => p.Difficulty)
                .HasConversion(
                    v => v.ToString(),
                    v => (ProblemDifficulty)Enum.Parse(typeof(ProblemDifficulty), v));//Converts the database string FROM the database back TO your C# enum
                    /*
                    Enum.Parse(typeof(ProblemDifficulty), v)
                    Enum.Parse(...): This is a built-in C# method that does the opposite of .ToString(). 
                    It takes a string and tries to find a matching value in an enum.
                    typeof(ProblemDifficulty): This is the first argument. It tells the method, 
                    "The enum I want you to search inside is ProblemDifficulty."
                    v: This is the second argument. It's the string we're giving it to parse (e.g., "Easy").
                    So, Enum.Parse(typeof(ProblemDifficulty), "Easy") finds the matching enum and returns it.
                    But there's one small catch: Enum.Parse is an old method, so it returns a generic object type, 
                    not a specific ProblemDifficulty type.
                    */

            modelBuilder.Entity<Submission>()
                .Property(s => s.Status)
                .HasConversion(
                    v => v.ToString(),
                    v => (SubmissionStatus)Enum.Parse(typeof(SubmissionStatus), v));

            modelBuilder.Entity<Vote>()
                .Property(v => v.EntityType)
                .HasConversion(
                    v => v.ToString(),
                    v => (VotableEntityType)Enum.Parse(typeof(VotableEntityType), v));

            // 4. Configure Unique Constraints (as discussed with User.cs)
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
            //HasIndex create a separate, sorted list(the index) that just contains this field and points to the user's row to enable fast searching
                .HasIndex(u => u.Email) 
                .IsUnique();

            // 5. Configure the Vote entity's polymorphic relationship
            // We can add an index to quickly find all votes for a given entity (Solution or Comment)
            modelBuilder.Entity<Vote>()
                .HasIndex(v => new { v.EntityId, v.EntityType });
        }
        
    }
}