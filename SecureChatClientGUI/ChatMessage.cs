// File: ChatMessage.cs
using System;
using System.Windows.Media.Imaging;
using System.ComponentModel; 
namespace SecureChatClientGUI
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private bool _isRecalled = false;
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        //Dinh dnah cho tin nhan
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        //Dung de an noi dung va hien thi thong bao tin nhan da bi thu hoi
        public bool IsRecalled
        {
            get => _isRecalled;
            set
            {
                if (_isRecalled != value)
                {
                    _isRecalled = value;
                    OnPropertyChanged(nameof(IsRecalled));
                    
                    OnPropertyChanged(nameof(DisplayContent));
                }
            }
        }
        public string? DisplayContent
        {
            get
            {
                if (IsRecalled)
                {
                    return "Tin nhắn đã bị thu hồi";
                }
                return Content;
            }
        }
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
