// File: ChatMessage.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace SecureChatClientGUI
{
    // BẮT BUỘC phải triển khai INotifyPropertyChanged
    public class ChatMessage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // --- Các thuộc tính cần thiết cho Chat Client Service ---

        public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
        public string Sender { get; set; } = string.Empty;
    // Nếu đây là tin nhắn nhóm, GroupId sẽ khác null/empty
    public string? GroupId { get; set; }
    // Tùy chọn: tên nhóm (dùng hiển thị)
    public string? GroupName { get; set; }
    // Cờ đánh dấu đây có phải là tin nhắn nhóm hay không
    public bool IsGroupMessage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsMine { get; set; }

        private string? _content;
        public string? Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayContent)); // Cần thông báo khi Content thay đổi
                }
            }
        }

        private BitmapImage? _image;
        public BitmapImage? Image
        {
            get => _image;
            set
            {
                if (_image != value)
                {
                    _image = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isRecalled;
        public bool IsRecalled
        {
            get => _isRecalled;
            set
            {
                if (_isRecalled != value)
                {
                    _isRecalled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayContent)); // Cần thông báo khi thu hồi
                }
            }
        }

        // Thuộc tính hiển thị cho XAML: Hiển thị nội dung hoặc thông báo thu hồi
        public string DisplayContent
        {
            get
            {
                if (IsRecalled)
                {
                    // LƯU Ý: Đây là nội dung hiển thị cho cả tin nhắn đi và đến
                    return "Tin nhắn đã bị thu hồi";
                }
                return Content ?? string.Empty;
            }
        }
    }
}