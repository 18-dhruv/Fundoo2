namespace Fundoo.User.Api.Contracts
{
    public class Enable2FARequest
    {
    }

    public class Enable2FAResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class Confirm2FARequest
    {
        public string Code { get; set; } = string.Empty;
    }

    public class Confirm2FAResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class Verify2FARequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class Verify2FAResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? AccessToken { get; set; }
        public Guid? UserId { get; set; }
    }

    public class Disable2FARequest
    {
    }

    public class Disable2FAResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
