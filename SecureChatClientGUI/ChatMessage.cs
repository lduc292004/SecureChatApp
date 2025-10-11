// File: ChatMessage.cs
namespace SecureChatClientGUI
{
    public class ChatMessage
    {
        public string Content { get; set; }

        // Thuộc tính quan trọng cho việc căn chỉnh trong XAML
        public bool IsMine { get; set; }

        public string Sender { get; set; }
    }
}