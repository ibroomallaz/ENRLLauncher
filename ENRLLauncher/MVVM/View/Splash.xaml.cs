
namespace ENRLLauncher.MVVM.View;

public partial class SplashWindow
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public void UpdateStatus(string text)
    {
        if (Dispatcher.CheckAccess())
        {
            StatusText?.Text = text;
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusText?.Text = text;
            }));
        }
    }
}
