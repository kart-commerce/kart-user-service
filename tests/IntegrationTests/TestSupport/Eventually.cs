namespace Kart.User.IntegrationTests.TestSupport;

/// <summary>
/// Polls for an eventually-consistent condition — this service's own CQRS read projection is
/// asynchronous by design (requirement-spec.md §3 Consistency row), so tests assert convergence
/// within a bound rather than requiring synchronous read-your-writes.
/// </summary>
public static class Eventually
{
    public static async Task<T> Assert<T>(Func<Task<T?>> probe, Func<T, bool> isSatisfied, TimeSpan? timeout = null) where T : class
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            var result = await probe();
            if (result is not null && isSatisfied(result))
            {
                return result;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("Condition was not satisfied within the timeout.");
    }
}
