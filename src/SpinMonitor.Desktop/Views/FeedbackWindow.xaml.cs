using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using SpinMonitor.Services;

namespace SpinMonitor.Views
{
    public partial class FeedbackWindow : Window
    {
        private readonly FeedbackService _svc;

        // POCO for WPF binding
        public sealed class FeedbackRow
        {
            public string File { get; set; } = "";
            public string Stream { get; set; } = "";
            public DateTime? CreationDate { get; set; }
        }

        private readonly ObservableCollection<FeedbackRow> _rows = new();

        public FeedbackWindow(FeedbackService svc)
        {
            InitializeComponent();
            _svc = svc;
            Grid.ItemsSource = _rows;
            Refresh();
        }

        public void Refresh()
        {
            _rows.Clear();
            foreach (var (file, stream, creationDate) in _svc.Snapshot())
                _rows.Add(new FeedbackRow { File = file, Stream = stream, CreationDate = creationDate });

            // keep alphabetical by File
            var sorted = _rows.OrderBy(r => r.File).ToList();
            _rows.Clear();
            foreach (var r in sorted) _rows.Add(r);
        }
    }
}