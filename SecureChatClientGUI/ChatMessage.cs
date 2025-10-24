// File: ChatMessage.cs
using System;
using System.Windows.Media.Imaging;

namespace SecureChatClientGUI
{
    public class ChatMessage
    {
        // Nội dung tin nhắn văn bản (nếu có)
        public string? Content { get; set; }

        // Thông tin người gửi
        public string? Sender { get; set; }

        // Xác định tin nhắn này có phải của chính mình hay không (để căn chỉnh UI)
        public bool IsMine { get; set; }

        // Hình ảnh đính kèm (nếu là tin nhắn hình)
        public BitmapImage? Image { get; set; }

        // Kiểm tra có hình hay không (tiện cho Binding)
        public bool HasImage => Image != null;

        // Ngày giờ gửi tin (nếu cần)
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
