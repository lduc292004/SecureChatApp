using Microsoft.Win32; // Cần thiết cho OpenFileDialog
using System.Collections.ObjectModel; // Cần thiết cho ObservableCollection
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls; // Cần thiết cho ListBox

// Đảm bảo namespace này khớp với project của bạn
namespace SecureChatClientGUI
{
    public partial class MainWindow : Window
    {
        // Khởi tạo ChatClientService ở đây (Sẽ được gán giá trị khi nhấn Connect)
        private ChatClientService? _chatService;

        public MainWindow()
        {
            InitializeComponent();
            // KHÔNG GÁN this.DataContext = this; ở đây. Sẽ gán sau khi khởi tạo _chatService.
            // Điều này giải quyết vấn đề expression-bodied properties bị lỗi.

            // Đăng ký sự kiện Loaded để ListBox có thể tự động cuộn khi Collection thay đổi
            // Cần đảm bảo ListBox trong XAML có Name="ChatListBox"
            ChatListBox.Loaded += ChatListBox_Loaded;
        }

        // Loại bỏ các Expression-bodied properties bị lỗi:
        // public ObservableCollection<string> Messages => _chatService?.Messages ?? new ObservableCollection<string>();
        // public ObservableCollection<string> OnlineUsers => _chatService?.OnlineUsers ?? new ObservableCollection<string>();


        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            string userName = UserNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                MessageBox.Show("Vui long nhap ten cua ban.", "Loi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Đảm bảo ChatClientService đã được định nghĩa đúng namespace
            _chatService = new ChatClientService(userName);

            // Đính kèm sự kiện cho các thông báo từ Service
            _chatService.StatusChanged += (status) => Dispatcher.Invoke(() => StatusTextBlock.Text = $"Trang thai: {status}");

            // LƯU Ý: Đã xóa dòng sau: _chatService.MessageReceived += (msg) => Dispatcher.Invoke(() => Messages.Add(msg));
            // Vì service hiện tại tự thêm ChatMessage vào Collection.

            // Xóa các tin nhắn cũ và danh sách online users khi kết nối mới
            _chatService.Messages.Clear();
            _chatService.OnlineUsers.Clear();

            // Cập nhật DataContext để ListBox nhận ObservableCollection mới
            // DataContext trỏ tới Service, nên ListBox binding ItemsSource="{Binding Messages}" sẽ hoạt động.
            this.DataContext = _chatService;

            await _chatService.ConnectAsync();
            UpdateControlStates(); // Cập nhật trạng thái nút
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            _chatService?.Disconnect();
            UpdateControlStates(); // Cập nhật trạng thái nút
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (_chatService == null || !_chatService.IsConnected) return;

            string message = MessageTextBox.Text;
            if (string.IsNullOrWhiteSpace(message)) return;

            // Service sẽ gửi tin nhắn và tự động thêm ChatMessage (IsMine=True) vào Collection
            await _chatService.SendMessageAsync(message);
            MessageTextBox.Clear();

            // Cuộn xuống dòng cuối cùng sau khi gửi
            ScrollToBottom();
        }

        // Xử lý phim Enter để gửi tin nhắn
        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendButton_Click(sender, e);
                e.Handled = true; // Ngăn không cho sự kiện Enter thực hiện thêm hành động
            }
        }

        private async void SendFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_chatService == null || !_chatService.IsConnected) return;

            OpenFileDialog openFileDialog = new OpenFileDialog();
            // Đặt filter để người dùng dễ chọn file hơn
            openFileDialog.Filter = "All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                // Gửi lệnh gửi file qua service
                await _chatService.SendMessageAsync($"/sendfile \"{filePath}\"");
            }
        }

        // Hàm cuộn xuống cuối cùng
        private void ScrollToBottom()
        {
            // Phải dùng Dispatcher.Invoke vì có thể được gọi từ nhiều luồng khác nhau
            Dispatcher.Invoke(() =>
            {
                if (ChatListBox.Items.Count > 0)
                {
                    // Lấy item cuối cùng và cuộn tới đó
                    ChatListBox.ScrollIntoView(ChatListBox.Items[ChatListBox.Items.Count - 1]);
                }
            });
        }

        // Sự kiện tự động cuộn khi ListBox được tải
        private void ChatListBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                // Khi một item mới được thêm vào collection, ta cuộn xuống
                if (_chatService != null)
                {
                    _chatService.Messages.CollectionChanged += (s, args) =>
                    {
                        if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                        {
                            ScrollToBottom();
                        }
                    };
                }
            }
        }

        // Cập nhật trạng thái các nút dựa trên kết nối
        private void UpdateControlStates()
        {
            bool isConnected = _chatService?.IsConnected ?? false;

            // Phải dùng Dispatcher.Invoke vì hàm này có thể được gọi từ background thread (từ ChatClientService)
            Dispatcher.Invoke(() =>
            {
                ConnectButton.IsEnabled = !isConnected;
                DisconnectButton.IsEnabled = isConnected;
                SendButton.IsEnabled = isConnected;
                SendFileButton.IsEnabled = isConnected;
                UserNameTextBox.IsEnabled = !isConnected;

                if (!isConnected)
                {
                    StatusTextBlock.Text = "Trang thai: Da ngat ket noi";
                }
            });
        }

        private void OnlineUsersListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Thêm logic xử lý khi người dùng chọn một user trong danh sách
        }
    }
}
