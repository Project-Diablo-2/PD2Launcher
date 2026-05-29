using System.Windows;
using System.Windows.Controls;

namespace PD2Launcherv2.CustomControl
{
    public class BlankButtonWithText : Button
    {
        static BlankButtonWithText()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlankButtonWithText), new FrameworkPropertyMetadata(typeof(BlankButtonWithText)));
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text",
            typeof(string),
            typeof(BlankButtonWithText),
            new PropertyMetadata("..."));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
    }
}
