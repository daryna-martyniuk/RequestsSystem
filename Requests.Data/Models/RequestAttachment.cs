using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Requests.Data.Models
{
    public class RequestAttachment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int RequestId { get; set; }
        [ForeignKey("RequestId")]
        public virtual Request Request { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        public byte[]? FileData { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}