using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LauncherLauncher.Views.Dialogs
{
    public enum DialogChoice
    {
        Cancel,
        Secondary,
        Primary
    }

    public partial class MessageDialog : UserControl
    {
        public event Action<DialogChoice> Chosen;

        public MessageDialog()
        {
            InitializeComponent();
        }

        public MessageDialog(string title, string message, string primary, string secondary, string cancelText, bool danger)
            : this()
        {
            TitleText.Text = string.IsNullOrEmpty(title) ? "确认" : title;
            MessageText.Text = message ?? "";

            PrimaryBtn.Content = string.IsNullOrEmpty(primary) ? "好" : primary;
            PrimaryBtn.Style = (Style)FindResource(danger ? "Btn.Danger" : "Btn.Primary");

            if (string.IsNullOrEmpty(secondary))
            {
                SecondaryBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                SecondaryBtn.Content = secondary;
            }

            if (string.IsNullOrEmpty(cancelText))
            {
                CancelBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                CancelBtn.Content = cancelText;
            }

            if (danger)
            {
                IconBadge.Background = (Brush)FindResource("Brush.DangerSoft");
                IconGlyph.Foreground = (Brush)FindResource("Brush.Danger");
                IconGlyph.Text = "\uE7BA";
            }
        }

        private void Primary_Click(object sender, RoutedEventArgs e)
        {
            Raise(DialogChoice.Primary);
        }

        private void Secondary_Click(object sender, RoutedEventArgs e)
        {
            Raise(DialogChoice.Secondary);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Raise(DialogChoice.Cancel);
        }

        private void Raise(DialogChoice choice)
        {
            var handler = Chosen;
            if (handler != null) handler(choice);
        }
    }
}
