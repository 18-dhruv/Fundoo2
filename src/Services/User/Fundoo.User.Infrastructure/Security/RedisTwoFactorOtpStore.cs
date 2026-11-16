using Fundoo.User.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace Fundoo.User.Infrastructure.Security;

public sealed class RedisTwoFactorOtpStore(
    IConnectionMultiplexer redis,
    IConfiguration configuration) : ITwoFactorOtpStore
{
    private const string KeyPrefix = "fundoo:2fa:otp:";
    private const string IssueScript = """
        redis.call('HSET', KEYS[1], 'code_hash', ARGV[1], 'attempts', 0)
        redis.call('EXPIRE', KEYS[1], ARGV[2])
        return 1
        """;

    private const string VerifyScript = """
        if redis.call('EXISTS', KEYS[1]) == 0 then return 0 end
        local attempts = tonumber(redis.call('HGET', KEYS[1], 'attempts') or '0')
        if attempts >= 3 then return 2 end
        if redis.call('HGET', KEYS[1], 'code_hash') == ARGV[1] then
            redis.call('DEL', KEYS[1])
            return 1
        end
        attempts = redis.call('HINCRBY', KEYS[1], 'attempts', 1)
        if attempts >= 3 then return 2 end
        return -1
        """;

    private readonly IDatabase _database = redis.GetDatabase();
    private readonly byte[] _hashKey = CreateHashKey(configuration);

    public async Task IssueAsync(Guid userId, string code, TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(userId);
        var ttlSeconds = Math.Max(1, (int)Math.Ceiling(lifetime.TotalSeconds));
        await _database.ScriptEvaluateAsync(
            IssueScript,
            new RedisKey[] { key },
            new RedisValue[] { HashCode(code), ttlSeconds })
            .WaitAsync(cancellationToken);
    }

    public async Task<OtpChallengeValidationResult> ValidateAndConsumeAsync(
        Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var result = await _database.ScriptEvaluateAsync(
            VerifyScript,
            new RedisKey[] { GetKey(userId) },
            new RedisValue[] { HashCode(code.Trim()) })
            .WaitAsync(cancellationToken);

        return (long)result switch
        {
            1 => OtpChallengeValidationResult.Success,
            2 => OtpChallengeValidationResult.AttemptsExceeded,
            -1 => OtpChallengeValidationResult.InvalidCode,
            _ => OtpChallengeValidationResult.InvalidOrExpired
        };
    }

    public async Task InvalidateAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(GetKey(userId)).WaitAsync(cancellationToken);
    }

    private string HashCode(string code)
    {
        var payload = Encoding.UTF8.GetBytes($"Fundoo:2FA:OTP:v1:{code}");
        return Convert.ToHexString(HMACSHA256.HashData(_hashKey, payload));
    }

    private static byte[] CreateHashKey(IConfiguration configuration)
    {
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("JWT signing key is not configured.");

        return SHA256.HashData(Encoding.UTF8.GetBytes($"Fundoo:2FA:OTP:hash-key:{signingKey}"));
    }

    private static RedisKey GetKey(Guid userId) => $"{KeyPrefix}{userId:N}";
}
