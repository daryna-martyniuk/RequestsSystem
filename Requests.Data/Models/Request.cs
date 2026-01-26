using Azure.Core;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Requests.Data.Models
{
    public class Request
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? Deadline { get; set; }
        public DateTime? CompletedAt { get; set; }

        public bool IsStrategic { get; set; }

        // Foreign Keys
        public int GlobalStatusId { get; set; }
        [ForeignKey("GlobalStatusId")]
        public virtual RequestStatus GlobalStatus { get; set; } = null!;

        public int PriorityId { get; set; }
        [ForeignKey("PriorityId")]
        public virtual RequestPriority Priority { get; set; } = null!;

        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual RequestCategory Category { get; set; } = null!;

        public int AuthorId { get; set; }
        [ForeignKey("AuthorId")]
        public virtual User Author { get; set; } = null!;


        public virtual ICollection<RequestComment> Comments { get; set; } = new List<RequestComment>();
        public virtual ICollection<DepartmentTask> DepartmentTasks { get; set; } = new List<DepartmentTask>();
        public virtual ICollection<RequestAttachment> Attachments { get; set; } = new List<RequestAttachment>();
    }
}