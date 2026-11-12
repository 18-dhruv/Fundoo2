using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.User.Domain.Entity
{
    public class AppUser
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string NormalizedEmail { get; set; } = "";   // Email.Trim().ToUpperInvariant()
        public string PasswordHash { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        // Two-Factor Authentication properties
        public bool TwoFactorEnabled { get; set; } = false;
        public string? TwoFactorSecret { get; set; } = null;

    }
}
