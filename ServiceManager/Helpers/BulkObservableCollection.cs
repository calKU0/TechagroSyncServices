using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ServiceManager.Helpers
{
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppress;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppress) base.OnCollectionChanged(e);
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) return;
            _suppress = true;
            foreach (var i in items) Items.Add(i);
            _suppress = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void InsertRange(int index, IEnumerable<T> items)
        {
            if (items == null) return;
            _suppress = true;
            int i = index;
            foreach (var it in items) Items.Insert(i++, it);
            _suppress = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void RemoveRange(int index, int count)
        {
            if (count <= 0 || index < 0 || index >= Items.Count) return;
            _suppress = true;
            for (int i = 0; i < count && index < Items.Count; i++)
            {
                Items.RemoveAt(index);
            }
            _suppress = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}