using Fundoo.User.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Fundoo.User.Infrastructure.Security
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddUserInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("UserDb")
                ?? throw new InvalidOperationException("UserDb connection string is not configured.");

            services.AddDbContext<UserDbContext>(options =>options.UseNpgsql(connectionString));

            var redisConnectionString = configuration["Redis:ConnectionString"]
                ?? throw new InvalidOperationException("Redis connection string is not configured.");
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redisConnectionString));

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
            services.AddScoped<ITokenService, JwtTokenService>();
            services.AddScoped<ITwoFactorAuthService, TwoFactorAuthService>();
            services.AddScoped<ITwoFactorOtpStore, RedisTwoFactorOtpStore>();

            services.AddScoped<AuthService>();
            services.AddScoped<IAuthService>(provider => provider.GetRequiredService<AuthService>());
            services.AddScoped<IExternalAuthService>(provider => provider.GetRequiredService<AuthService>());

            return services;
        }
    }
}
