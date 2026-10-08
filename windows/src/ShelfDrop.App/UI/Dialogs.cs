using System;
using ShelfDrop.Core;

namespace ShelfDrop.App.UI
{
    internal static class Dialogs
    {
        public static void Tell(string title, string message, bool isWarning = true)
        {
            System.Windows.MessageBox.Show(
                message, title, System.Windows.MessageBoxButton.OK,
                isWarning ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
        }

        /// <summary>
        /// Asks before uninstalling. Cancel is the default, so that a stray press of Enter picks the safe choice.
        /// </summary>
        public static bool ConfirmUninstall(UninstallConfirmation confirmation)
        {
            try
            {
                var cancel = new System.Windows.Forms.TaskDialogButton(confirmation.CancelButton);
                var confirm = new System.Windows.Forms.TaskDialogButton(confirmation.ConfirmButton);
                var page = new System.Windows.Forms.TaskDialogPage
                {
                    Caption = "ShelfDrop",
                    Heading = confirmation.Title,
                    Text = confirmation.Message,
                    Icon = System.Windows.Forms.TaskDialogIcon.Warning,
                    AllowCancel = true,
                    Buttons = { cancel, confirm },
                    DefaultButton = cancel,
                };
                return System.Windows.Forms.TaskDialog.ShowDialog(page) == confirm;
            }
            catch (Exception e)
            {
                // The nicer dialog needs a recent common-controls library; the plain one always works.
                Log.Error("the task dialog failed; using a plain message box", e);
                System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show(
                    confirmation.Message + "\n\nPress OK to " + confirmation.ConfirmButton.ToLowerInvariant() + ".",
                    confirmation.Title,
                    System.Windows.MessageBoxButton.OKCancel,
                    System.Windows.MessageBoxImage.Warning,
                    System.Windows.MessageBoxResult.Cancel);
                return result == System.Windows.MessageBoxResult.OK;
            }
        }
    }
}
