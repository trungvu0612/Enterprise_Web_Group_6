using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Identity;

namespace TCS2010NX.Models
{
    public enum ContributionStatus { Approved, Pending, Rejected }
    public enum FileType { Document, Image }

    public static class _Global
    {
        private static string rootFolderName => "_Files";

        public static string PATH_TOPIC => Path.Combine(rootFolderName, "Topics");
    }

    public class CUser : IdentityUser
    {
        public string Number { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        // Some more...

        public int DepartmentId { get; set; }
        public virtual Department Department { get; set; }

        public virtual ICollection<Contribution> Contributions { get; set; }
        public virtual ICollection<Comment> Comments { get; set; }
    }

    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public virtual ICollection<CUser> Users { get; set; }
    }

    public class Topic
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime Deadline_1 { get; set; }
        public DateTime Deadline_2 { get; set; }
        public DateTime CreationDate { get; set; }
        // Deadline_2, CreationDate...

        public virtual ICollection<Contribution> Contributions { get; set; }
    }

    public class Contribution
    {
        public int Id { get; set; }
        public ContributionStatus Status { get; set; }

        public string ContributorId { get; set; }
        public virtual CUser Contributor { get; set; }

        public int TopicId { get; set; }
        public virtual Topic Topic { get; set; }

        public virtual ICollection<SubmittedFile> Files { get; set; }
        public virtual ICollection<Comment> Comments { get; set; }
    }

    public class SubmittedFile
    {
        public int Id { get; set; }
        public string URL { get; set; }
        public FileType Type { get; set; }

        public int ContributionId { get; set; }
        public virtual Contribution Contribution { get; set; }
    }

    public class Comment
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime Date { get; set; }

        public int ContributionId { get; set; }
        public virtual Contribution Contribution { get; set; }

        public string UserId { get; set; }
        public virtual CUser User { get; set; }
    }
}