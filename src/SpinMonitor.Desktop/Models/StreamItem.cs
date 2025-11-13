using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SpinMonitor.Models
{
    public class StreamItem : INotifyPropertyChanged
    {
        private string _name = "";
        private string _url = "";
        private bool   _enabled = true;
        private string _status = "Idle";
        private int    _detections;
        private double _mbps;
        private bool   _active;
        private int    _reconnects;
        private bool   _autoReconnect = true;
        private string? _streamType;
        private string? _streamNumber;

        public string Name 
        { 
            get => _name; 
            set { _name = value; OnPropertyChanged(); } 
        }

        public string Url 
        { 
            get => _url; 
            set { _url = value; OnPropertyChanged(); } 
        }

        public bool IsEnabled 
        { 
            get => _enabled; 
            set { _enabled = value; OnPropertyChanged(); } 
        }

        public string Status 
        { 
            get => _status; 
            set { _status = value; OnPropertyChanged(); } 
        }

        public int Detections 
        { 
            get => _detections; 
            set { _detections = value; OnPropertyChanged(); } 
        }

        public double BandwidthMBps 
        { 
            get => _mbps; 
            set { _mbps = value; OnPropertyChanged(); } 
        }

        public bool IsActive 
        { 
            get => _active; 
            set { _active = value; OnPropertyChanged(); } 
        }

        public int Reconnects 
        { 
            get => _reconnects; 
            set { _reconnects = value; OnPropertyChanged(); } 
        }

        public bool AutoReconnect 
        { 
            get => _autoReconnect; 
            set { _autoReconnect = value; OnPropertyChanged(); } 
        }

        public string? StreamType
        {
            get => _streamType;
            set { _streamType = value; OnPropertyChanged(); }
        }

        public string? StreamNumber
        {
            get => _streamNumber;
            set { _streamNumber = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}