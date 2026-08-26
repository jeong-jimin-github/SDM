using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SDM.Core.Models;

namespace SDM.App.Converters;

public sealed class BoolToVis : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var flag = value is true;
        if (Invert) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        return value is DownloadStatus s
            ? s switch
            {
                DownloadStatus.Downloading or DownloadStatus.Connecting => Brush("#059669"),
                DownloadStatus.Completed => Brush("#34D399"),
                DownloadStatus.Paused or DownloadStatus.Scheduled => Brush("#D97706"),
                DownloadStatus.Failed => Brush("#DC2626"),
                DownloadStatus.Canceled => Brush("#64748B"),
                _ => Brush("#64748B")
            }
            : Brush("#64748B");
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();

    private static SolidColorBrush Brush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        b.Freeze();
        return b;
    }
}

public sealed class NullToVis : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is string s ? (string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible)
        : value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
