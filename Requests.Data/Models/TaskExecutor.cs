using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Requests.Data.Models
{
    public class TaskExecutor
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public bool IsLead { get; set; }
        public DateTime? AssignedAt { get; set; } = DateTime.Now;

        public int DepartmentTaskId { get; set; }
        [ForeignKey("DepartmentTaskId")]
        public virtual DepartmentTask DepartmentTask { get; set; } = null!;

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}