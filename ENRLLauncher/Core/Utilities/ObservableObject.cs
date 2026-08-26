using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ENRLLauncher.Core.Utilities
{
    //from https://github.com/ibroomallaz/Desktop-Support/blob/MVVM/DSAMVVM/Core/Utilities/ObservableObject.cs
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Basic Set: compares, assigns, notifies
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // Set with callback (e.g., to sync related props)
        protected bool Set<T>(ref T field, T value, Action onChanged, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}