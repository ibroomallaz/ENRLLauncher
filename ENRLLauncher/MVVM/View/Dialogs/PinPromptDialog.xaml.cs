using System.Windows;
using System.Windows.Input;
using ENRLLauncher.MVVM.ViewModel.Dialogs;

namespace ENRLLauncher.MVVM.View.Dialogs;

public partial class PinPromptDialog
{
    private PinPromptDialogViewModel? ViewModel => DataContext as PinPromptDialogViewModel;

    public PinPromptDialog()
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
        FocusActiveInput();
    }

    private void FocusActiveInput()
    {
        if (ViewModel == null) return;

        if (ViewModel.IsVerifyMode)
        {
            VerifyPinBox.Focus();
        }
        else if (ViewModel.IsSetupMode)
        {
            SetupPinBox.Focus();
        }
        else if (ViewModel.IsChangeMode)
        {
            ChangeCurrentPinBox.Focus();
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ViewModel?.CloseCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ExecuteCurrentSubmit();
            e.Handled = true;
        }
    }

    private void ExecuteCurrentSubmit()
    {
        if (ViewModel == null || ViewModel.IsBusy) return;

        if (ViewModel.IsVerifyMode)
        {
            ViewModel.SubmitVerify(VerifyPinBox.Password);
        }
        else if (ViewModel.IsSetupMode)
        {
            ViewModel.SubmitSetup(SetupPinBox.Password, SetupConfirmPinBox.Password);
        }
        else if (ViewModel.IsChangeMode)
        {
            ViewModel.SubmitChange(ChangeCurrentPinBox.Password, ChangeNewPinBox.Password, ChangeConfirmPinBox.Password);
        }
    }

    private void OnSubmitVerifyClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.SubmitVerify(VerifyPinBox.Password);
    }

    private void OnSubmitSetupClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.SubmitSetup(SetupPinBox.Password, SetupConfirmPinBox.Password);
    }

    private void OnSubmitChangeClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.SubmitChange(ChangeCurrentPinBox.Password, ChangeNewPinBox.Password, ChangeConfirmPinBox.Password);
    }

    private void OnAdminOverrideClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.AdminOverrideCommand.Execute(null);
    }
}
