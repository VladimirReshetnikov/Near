using System;
using System.Threading;
using System.Threading.Tasks;

namespace Near.Services.Tasks;

public sealed record ProgressInfo(double? Percent, string Message);

public interface IBackgroundTask
{
    Guid Id { get; }

    string Title { get; }

    Task RunAsync(IProgress<ProgressInfo> progress, CancellationToken cancellationToken);
}

public sealed record TaskHandle(Guid Id, Task Task);

public interface ITaskRunner
{
    TaskHandle Enqueue(IBackgroundTask task, CancellationToken cancellationToken = default);
}

public sealed class TaskRunner : ITaskRunner
{
    private readonly SemaphoreSlim _semaphore;

    public TaskRunner(int maxConcurrency = 2)
    {
        if (maxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
        }

        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public TaskHandle Enqueue(IBackgroundTask task, CancellationToken cancellationToken = default)
    {
        if (task is null)
        {
            throw new ArgumentNullException(nameof(task));
        }

        var runTask = RunTaskAsync(task, cancellationToken);
        return new TaskHandle(task.Id, runTask);
    }

    private async Task RunTaskAsync(IBackgroundTask task, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var progress = new Progress<ProgressInfo>();
            await task.RunAsync(progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
