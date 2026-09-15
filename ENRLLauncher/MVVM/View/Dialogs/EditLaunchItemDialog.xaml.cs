using System.Windows;
using System.Windows.Input;
using ENRLLauncher.MVVM.ViewModel.Dialogs;

namespace ENRLLauncher.MVVM.View.Dialogs;

public partial class EditLaunchItemDialog
{
    private EditLaunchItemDialogViewModel? ViewModel => DataContext as EditLaunchItemDialogViewModel;

    public EditLaunchItemDialog()
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

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        TitleBox.Focus();
        TitleBox.SelectAll();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ViewModel?.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }
}
