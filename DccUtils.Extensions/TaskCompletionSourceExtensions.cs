namespace Dcc.Extensions;

public static class TaskCompletionSourceExtensions {
    public static async Task<TResult?> AwaitResult<TResult>(this TaskCompletionSource<Func<TResult>> source, int timeoutMillis = 5000, CancellationToken cancellationToken = default) {
        await Task.WhenAny(source.Task, Task.Delay(timeoutMillis, cancellationToken));
        if (!source.Task.IsCompletedSuccessfully) {
            return default;
        }

        var func = await source.Task;
        return func.Invoke();
    }

    public static async Task<TResult?> AwaitResult<T1, TResult>(this TaskCompletionSource<Func<T1, TResult>> source, T1 t1, int timeoutMillis = 5000, CancellationToken cancellationToken = default) {
        await Task.WhenAny(source.Task, Task.Delay(timeoutMillis, cancellationToken));
        if (!source.Task.IsCompletedSuccessfully) {
            return default;
        }

        var func = await source.Task;
        return func.Invoke(t1);
    }

    public static async Task<TResult?> AwaitResult<T1, T2, TResult>(this TaskCompletionSource<Func<T1, T2, TResult>> source, T1 t1, T2 t2, int timeoutMillis = 5000, CancellationToken cancellationToken = default) {
        await Task.WhenAny(source.Task, Task.Delay(timeoutMillis, cancellationToken));
        if (!source.Task.IsCompletedSuccessfully) {
            return default;
        }

        var func = await source.Task;
        return func.Invoke(t1, t2);
    }

}
