namespace Fundoo.Notification.Api.Services;

public sealed class SmtpBulkhead
{
    private readonly SemaphoreSlim _semaphore = new(5, 5);

    public async Task ExecuteAsync(Func<Task> operation)
    {
        // Try to acquire a permit without waiting.
        var acquired = await _semaphore.WaitAsync(0);

        if (!acquired)
        {
            throw new InvalidOperationException(
                "SMTP service is busy. Please try again later.");
        }

        try
        {
            await operation();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}