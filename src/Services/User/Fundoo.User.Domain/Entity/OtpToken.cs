namespace Fundoo.User.Domain.Entity
{
    public class OtpToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string OtpCode { get; set; } = string.Empty;
        public int Attempts { get; set; } = 0;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? VerifiedAtUtc { get; set; }
        public bool IsVerified { get; set; } = false;
    }
}
