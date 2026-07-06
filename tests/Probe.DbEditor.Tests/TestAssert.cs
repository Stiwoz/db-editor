namespace Probe.DbEditor.Tests;

internal static class TestAssert
{
    public static TException Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected {typeof(TException).Name}, got {ex.GetType().Name}: {ex.Message}");
        }

        Assert.Fail($"Expected {typeof(TException).Name}, but no exception was thrown.");
        throw new UnreachableException();
    }

    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected {typeof(TException).Name}, got {ex.GetType().Name}: {ex.Message}");
        }

        Assert.Fail($"Expected {typeof(TException).Name}, but no exception was thrown.");
        throw new UnreachableException();
    }

    private sealed class UnreachableException : Exception;
}
