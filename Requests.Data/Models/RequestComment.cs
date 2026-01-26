using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Requests.Data.Models
{
    public class RequestComment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string CommentText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int RequestId { get; set; }
        [ForeignKey("RequestId")]
        public virtual Request Request { get; set; } = null!;

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}