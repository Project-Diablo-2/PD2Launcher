using System.Windows;
using System.Windows.Controls;

namespace PD2Launcherv2.CustomControl
{
    public class BlankButtonWithText : Button
    {
        public enum ButtonKindEnum
        {
            Normal,
            SplitTop,
            SplitBottom
        }

        static BlankButtonWithText()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlankButtonWithText), new FrameworkPropertyMetadata(typeof(BlankButtonWithText)));
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text",
            typeof(string),
            typeof(BlankButtonWithText),
            new PropertyMetadata("..."));

        public static readonly DependencyProperty ButtonKindProperty = DependencyProperty.Register(
            "ButtonKind",
            typeof(ButtonKindEnum),
            typeof(BlankButtonWithText),
            new PropertyMetadata(ButtonKindEnum.Normal));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public ButtonKindEnum ButtonKind
        {
            get => (ButtonKindEnum)GetValue(ButtonKindProperty);
            set => SetValue(ButtonKindProperty, value);
        }
    }
}
