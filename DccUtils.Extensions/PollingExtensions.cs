using System.Collections.Concurrent;
using System.Diagnostics;

namespace Dcc.Extensions;

public static class PollingExtensions {
    public static async Task<T?> AwaitItem<T>(this ICollection<T> collection, Func<T, bool> selector, int pollingTimeout = 5000, int intervals = 10, CancellationToken cancellationToken = default) {
        var watch = new Stopwatch();
        watch.Start();

        while (!cancellationToken.IsCancellationRequested && watch.ElapsedMilliseconds < pollingTimeout) {
            var item = collection.FirstOrDefault(selector);
            if (item != null) {
                return item;
            }

            await Task.Delay(intervals, cancellationToken);
        }

        return default;
    }

    public static async Task<T?> AwaitItem<T>(this IQueryable<T> collection, Func<T, bool> selector, int pollingTimeout = 5000, int intervals = 10, CancellationToken cancellationToken = default) {
        var watch = new Stopwatch();
        watch.Start();

        while (!cancellationToken.IsCancellationRequested && watch.ElapsedMilliseconds < pollingTimeout) {
            var item = collection.FirstOrDefault(selector);
            if (item != null) {
                return item;
            }

            await Task.Delay(intervals, cancellationToken);
        }

        return default;
    }

    public static async Task<T?> AwaitItem<T>(this ConcurrentBag<T> collection, Func<T, bool> selector, int pollingTimeout = 5000, int intervals = 10, CancellationToken cancellationToken = default) {
        var watch = new Stopwatch();
        watch.Start();

        while (!cancellationToken.IsCancellationRequested && watch.ElapsedMilliseconds < pollingTimeout) {
            var item = collection.FirstOrDefault(selector);
            if (item != null) {
                return item;
            }

            await Task.Delay(intervals, cancellationToken);
        }

        return default;
    }
}
