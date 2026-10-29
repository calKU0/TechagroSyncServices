using System.Collections.Generic;

namespace TechagroSyncServices.Shared.DTOs
{
    // Items fetched from supplier API. IsComplete is false when any request failed,
    // so products missing in Items can't be treated as removed by the supplier.
    public class FetchResult<T>
    {
        public List<T> Items { get; }
        public bool IsComplete { get; }

        private FetchResult(List<T> items, bool isComplete)
        {
            Items = items ?? new List<T>();
            IsComplete = isComplete;
        }

        public static FetchResult<T> Complete(List<T> items) => new FetchResult<T>(items, true);

        public static FetchResult<T> Incomplete(List<T> items) => new FetchResult<T>(items, false);
    }
}
