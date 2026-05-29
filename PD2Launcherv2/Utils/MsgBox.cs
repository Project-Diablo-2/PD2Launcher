using System.Text;
using System.Windows;

namespace PD2Launcherv2.Utils
{
    public static class MsgBox
    {
        public const string DefaultDialogTitle = "Project Diablo 2 Launcher";

        private static MessageBoxResult ShowWrapper(string messageBoxText, MessageBoxImage icon, MessageBoxButton button, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            if (App.Current.MainWindow != null)
            {
                return MessageBox.Show(App.Current.MainWindow, messageBoxText, DefaultDialogTitle, button, icon, defaultResult);
            }
            else
            {
                return MessageBox.Show(messageBoxText, DefaultDialogTitle, button, icon, defaultResult);
            }
        }

        public static MessageBoxResult Info(string messageBoxText, MessageBoxButton button = MessageBoxButton.OK, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            return ShowWrapper(messageBoxText, MessageBoxImage.Information, button, defaultResult);
        }

        public static MessageBoxResult Warn(string messageBoxText, MessageBoxButton button = MessageBoxButton.OK, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            return ShowWrapper(messageBoxText, MessageBoxImage.Warning, button, defaultResult);
        }

        public static MessageBoxResult Error(string messageBoxText, MessageBoxButton button = MessageBoxButton.OK, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            return ShowWrapper(messageBoxText, MessageBoxImage.Error, button, defaultResult);
        }

        public static MessageBoxResult Exception(
            Exception? exception,
            string? messageBoxText = null,
            MessageBoxImage icon = MessageBoxImage.Error,
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            if (exception == null && messageBoxText == null)
            {
                throw new ArgumentNullException($"Method '{nameof(Exception)}' cannot be called with both: '{nameof(exception)}' and '{nameof(messageBoxText)}' being null.", (Exception)null!);
            }

            // Available options:
            //
            // = = = = = = = = = = =
            //
            //   [message/exception]
            //
            // = = = = = = = = = = =
            //
            //   [message]
            //
            //   [exception]
            //
            // = = = = = = = = = = =
            //
            //   [message/exception]
            //
            // ---
            //
            //   [exception]
            //
            //   [exception]
            //
            //   [...]
            //
            // = = = = = = = = = = =
            //
            // Conclusion: Add "---" at entry index 1 where total entry count >= 3

            int entryCount = messageBoxText != null ? 1 : 0;

            for (var currentException = exception!; currentException != null; currentException = currentException.InnerException)
            {
                if (currentException is AggregateException)
                {
                    // Ignore AggregateException exception itself, only its InnerExceptions count
                    continue;
                }

                if (++entryCount >= 3)
                {
                    // This much should be enough
                    break;
                }
            }

            StringBuilder sb = new(messageBoxText);

            var lastMessage = messageBoxText;
            var currEntryIdx = lastMessage == null ? 0 : 1;

            for (var currentException = exception!; currentException != null; currentException = currentException.InnerException)
            {
                if (currentException is AggregateException)
                {
                    // Ignore AggregateException exception itself, only its InnerExceptions contain useful messages
                    continue;
                }

                if (lastMessage == currentException.Message)
                {
                    // Skip duplicate messages
                    continue;
                }

                if (sb.Length > 0)
                {
                    if (entryCount >= 3 && currEntryIdx == 1)
                    {
                        sb.Append("\n\n---\n\n");
                    }
                    else
                    {
                        sb.Append("\n\n");
                    }
                }

                sb.Append(currentException.Message);
                lastMessage = currentException.Message;
                ++currEntryIdx;
            }

            return ShowWrapper(sb.ToString(), icon, button, defaultResult);
        }
    }
}
