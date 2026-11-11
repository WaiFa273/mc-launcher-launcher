using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LauncherLauncher.Util
{
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool && (bool)value;
            return b ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>体检分数：越高越绿，越低越红。</summary>
    public class ScoreToBrushConverter : IValueConverter
    {
        private static readonly System.Windows.Media.Brush Success = Frozen(0x12, 0x99, 0x6B);
        private static readonly System.Windows.Media.Brush Accent = Frozen(0x2E, 0x6B, 0xFF);
        private static readonly System.Windows.Media.Brush Warn = Frozen(0xC4, 0x76, 0x1A);
        private static readonly System.Windows.Media.Brush Danger = Frozen(0xE5, 0x48, 0x4D);

        private static System.Windows.Media.Brush Frozen(byte r, byte g, byte b)
        {
            var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int score = 0;
            try { score = System.Convert.ToInt32(value); } catch { }
            if (score >= 85) return Success;
            if (score >= 60) return Accent;
            if (score >= 35) return Warn;
            return Danger;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
