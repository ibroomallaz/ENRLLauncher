using System.Windows.Input;

namespace ENRLLauncher.MVVM.View.Dialogs;

public partial class VersionUpdateDialog
{
    public VersionUpdateDialog()
    {
        InitializeComponent();
    }

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
