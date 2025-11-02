using System;
using System.Windows;

namespace SecureChatClientGUI
{
    public partial class App : Application
    {
        // Thêm phương thức static này
        public static void SwitchTheme(bool isDarkTheme)
        {
            // Xóa theme cũ
            Current.Resources.MergedDictionaries.Clear();

            // Tạo đối tượng ResourceDictionary mới
            ResourceDictionary newTheme = new ResourceDictionary();

            if (isDarkTheme)
            {
                newTheme.Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative);
            }
            else
            {
                newTheme.Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative);
            }

            // Thêm theme mới vào
            Current.Resources.MergedDictionaries.Add(newTheme);
        }
    }
}