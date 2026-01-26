using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Requests.Data.Models
{
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int? RequestId { get; set; }
        [ForeignKey("RequestId")]
        public virtual Request? Request { get; set; }

        public int? UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [MaxLength(50)]
        public string Action { get; set; } = string.Empty; 

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}