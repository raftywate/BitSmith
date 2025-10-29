using System;
using dotnetBitSmith.Entities;
using dotnetBitSmith.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

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

            // 3. Configure Enums to be stored as strings (e.g., "Easy", "Pending")
            modelBuilder.Entity<Problem>()
                .Property(p => p.Difficulty)
                .HasConversion(
                    v => v.ToString(),
                    v => (ProblemDifficulty)Enum.Parse(typeof(ProblemDifficulty), v));

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
                .HasIndex(u => u.Email)
                .IsUnique();

            // 5. Configure the Vote entity's polymorphic relationship
            // We can add an index to quickly find all votes for a given entity (Solution or Comment)
            modelBuilder.Entity<Vote>()
                .HasIndex(v => new { v.EntityId, v.EntityType });
        }
        
    }
}