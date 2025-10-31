// File: NullToVisibilityConverter.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SecureChatClientGUI
{
    // Cần thiết cho việc ẩn/hiện TextBlock và Image dựa trên giá trị (Content/Image)
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Nếu giá trị là null, trả về Collapsed (ẩn)
            if (value == null)
            {
                return Visibility.Collapsed;
            }
            // Nếu là string rỗng cũng coi là ẩn
            if (value is string str && string.IsNullOrWhiteSpace(str))
            {
                return Visibility.Collapsed;
            }
            // Ngược lại, trả về Visible (hiển thị)
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}