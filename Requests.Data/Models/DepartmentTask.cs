using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Requests.Data.Models
{
    public class DepartmentTask
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public DateTime? AssignedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public int RequestId { get; set; }
        [ForeignKey("RequestId")]
        public virtual Request Request { get; set; } = null!;

        public int DepartmentId { get; set; }
        [ForeignKey("DepartmentId")]
        public virtual Department Department { get; set; } = null!;

        public int StatusId { get; set; }
        [ForeignKey("StatusId")]
        public virtual RequestStatus Status { get; set; } = null!;

        public virtual ICollection<TaskExecutor> Executors { get; set; } = new List<TaskExecutor>();
    }
}