using System.Windows.Controls;
using ENRLLauncher.MVVM.ViewModel;

namespace ENRLLauncher.MVVM.View
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext ??= new SettingsViewModel();
        }
    }
}
