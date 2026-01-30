using System.ComponentModel.DataAnnotations;

namespace ASAssignment.Model
{
    public class AuditLog
    {
        public int Id { get; set; }

        [StringLength(450)]
        public string? UserId { get; set; }

        [StringLength(256)]
        public string? Email { get; set; }

        [Required, StringLength(50)]
        public string Action { get; set; } = ""; // "LOGIN_SUCCESS", "LOGIN_FAIL", "LOGOUT", "LOCKED_OUT"

        public bool IsSuccess { get; set; }


        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
