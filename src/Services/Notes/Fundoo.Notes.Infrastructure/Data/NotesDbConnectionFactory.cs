using Npgsql;
using Microsoft.Extensions.Configuration;

namespace Fundoo.Notes.Infrastructure.Data;

public class NotesDbConnectionFactory
{
    private readonly string _connectionString;

    public NotesDbConnectionFactory(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("NotesDb")
            ?? throw new InvalidOperationException(
                "NotesDb connection string is missing.");
    }

    public NpgsqlConnection CreateConnection()
        => new NpgsqlConnection(_connectionString);
}