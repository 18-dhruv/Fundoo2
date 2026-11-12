namespace Fundoo.User.Domain.Entity
{
    public class ExternalLogin
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string Provider { get; set; } = "Google";

        public string ProviderSubject { get; set; } = "";

        public DateTime CreatedAtUtc { get; set; }
    }
}