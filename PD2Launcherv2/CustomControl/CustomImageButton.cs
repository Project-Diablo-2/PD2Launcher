using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PD2Launcherv2.CustomControl
{
    public class CustomImageButton : Button
    {
        private static RenderTargetBitmap _missingImage = null!;
        private static BitmapSource MissingImage
        {
            get
            {
                if (_missingImage == null)
                {
                    Rect imageSize = new(new Size(100, 100));

                    _missingImage = new RenderTargetBitmap((int)imageSize.Width, (int)imageSize.Height, 96, 96, PixelFormats.Pbgra32);

                    DrawingVisual visual = new();
                    using (DrawingContext context = visual.RenderOpen())
                    {
                        context.DrawRectangle(Brushes.Magenta, pen: null, imageSize);
                    }

                    _missingImage.Render(visual);
                }

                return _missingImage;
            }
        }

        static CustomImageButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomImageButton), new FrameworkPropertyMetadata(typeof(CustomImageButton)));
        }

        public static readonly DependencyProperty NormalImageSourceProperty = DependencyProperty.Register(
            "NormalImageSource",
            typeof(ImageSource),
            typeof(CustomImageButton),
            new PropertyMetadata(default(ImageSource)));

        public static readonly DependencyProperty PressedImageSourceProperty = DependencyProperty.Register(
            "PressedImageSource",
            typeof(ImageSource),
            typeof(CustomImageButton),
            new PropertyMetadata(default(ImageSource)));

        public static readonly DependencyProperty DisabledImageSourceProperty = DependencyProperty.Register(
            "DisabledImageSource",
            typeof(ImageSource),
            typeof(CustomImageButton),
            new PropertyMetadata(MissingImage));

        public ImageSource NormalImageSource
        {
            get => (ImageSource)GetValue(NormalImageSourceProperty);
            set => SetValue(NormalImageSourceProperty, value);
        }

        public ImageSource PressedImageSource
        {
            get => (ImageSource)GetValue(PressedImageSourceProperty);
            set => SetValue(PressedImageSourceProperty, value);
        }

        public ImageSource DisabledImageSource
        {
            get => (ImageSource)GetValue(DisabledImageSourceProperty);
            set => SetValue(DisabledImageSourceProperty, value);
        }
    }
}