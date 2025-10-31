// File: ChatTemplateSelector.cs
using System.Windows.Controls;
using System.Windows;

namespace SecureChatClientGUI
{
    public class ChatTemplateSelector : DataTemplateSelector
    {
        // Khai báo 2 Template mà bạn đã định nghĩa trong MainWindow.xaml
        public DataTemplate? MyMessageTemplate { get; set; }
        public DataTemplate? OtherMessageTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is ChatMessage message)
            {
                // Logic chính: Kiểm tra thuộc tính IsMine
                if (message.IsMine)
                {
                    // Tin nhắn của mình (Căn phải, màu xanh)
                    return MyMessageTemplate;
                }
                else
                {
                    // Tin nhắn của người khác (Căn trái, màu xám)
                    return OtherMessageTemplate;
                }
            }
            // Quan trọng: Trả về null nếu không phải ChatMessage để tránh lỗi
            return null;
        }
    }
}