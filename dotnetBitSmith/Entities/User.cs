using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotnetBitSmith.Entities {
    public class User {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; }

        [Required]
        [StringLength(20)]
        public string UserRole { get; set; } = "User";
        
        [Required]
        public string PasswordHash { get; set; }

        [StringLength(100)]
        public string? DisplayName { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        [StringLength(512)]
        public string? ProfilePictureUrl { get; set; }

        public bool IsEmailVerified { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        //---Navigation Properties---
        //virtual here is for efficiency since it enables lazy loading. 
        //This proxy code will intercept the request and "lazily" run a separate query against the database to fetch all the snippets for that user.
        public virtual ICollection<CodeSnippet> CodeSnippets { get; set; } = new List<CodeSnippet>();
        public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();

        [NotMapped] //Not stored in the database, just a helper method to check if the user is Admin
        public bool IsAdmin => UserRole.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        
    }
}