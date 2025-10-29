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
            this.Closing += MainWindow_Closing;
            ChatListBox.Loaded += ChatListBox_Loaded;

            // Khởi tạo trạng thái điều khiển ban đầu
            UpdateControlStates();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Gọi Disconnect để đóng TcpClient và SslStream một cách sạch sẽ
            _chatService?.Disconnect();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            string userName = UserNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                MessageBox.Show("Vui lòng nhập tên của bạn.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _chatService = new ChatClientService(userName);

            // Đính kèm sự kiện cho các thông báo từ Service
            _chatService.StatusChanged += (status) => Dispatcher.Invoke(() => StatusTextBlock.Text = $"Trạng thái: {status}");

            // Xóa các tin nhắn cũ và danh sách online users khi kết nối mới
            _chatService.Messages.Clear();
            _chatService.OnlineUsers.Clear();

            // Cập nhật DataContext
            this.DataContext = _chatService;

            // Cố gắng kết nối
            bool connected = await _chatService.ConnectAsync();
            UpdateControlStates(); // Cập nhật trạng thái nút

            // ⭐ BƯỚC SỬA 1: GỬI TÊN LÊN SERVER NGAY SAU KHI KẾT NỐI THÀNH CÔNG
            if (connected)
            {
                string setNameCommand = $"[SET_NAME]:{userName}";
                await _chatService.SendMessageAsync(setNameCommand);
            }
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
            // Đặt filter chỉ cho phép chọn file ảnh (khuyến nghị)
            openFileDialog.Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                // ⭐ BƯỚC SỬA 2: GỌI TRỰC TIẾP SendImageAsync
                // Loại bỏ việc gửi lệnh "/sendfile" qua SendMessageAsync
                await _chatService.SendImageAsync(filePath);

                // Cuộn xuống sau khi gửi
                ScrollToBottom();
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
                // Chỉ đăng ký sự kiện nếu _chatService đã tồn tại
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
                // Nếu ChatListBox.Loaded được gọi trước ConnectButton_Click, sự kiện sẽ được gán lại
                // sau khi _chatService được khởi tạo và DataContext được thiết lập.
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

                if (!isConnected && _chatService == null)
                {
                    StatusTextBlock.Text = "Trạng thái: Chưa kết nối";
                }
                else if (!isConnected)
                {
                    StatusTextBlock.Text = "Trạng thái: Đã ngắt kết nối";
                }
            });
        }

        private void OnlineUsersListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Thêm logic xử lý khi người dùng chọn một user trong danh sách
        }

        private async void btnThuHoi_Click(object sender, RoutedEventArgs e)
        {
            if (_chatService == null || !_chatService.IsConnected)
            {
                MessageBox.Show("Chưa kết nối đến máy chủ.", "Lỗi Thu Hồi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // 1. Lấy tin nhắn đang được chọn từ ListBox
            if (ChatListBox.SelectedItem is ChatMessage messageToRecall)
            {
                // 2. Kiểm tra an toàn: Phải là tin nhắn của mình và chưa bị thu hồi
                if (!messageToRecall.IsMine)
                {
                    MessageBox.Show("Bạn chỉ có thể thu hồi tin nhắn của chính mình.", "Lỗi Thu Hồi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (messageToRecall.IsRecalled)
                {
                    MessageBox.Show("Tin nhắn này đã bị thu hồi.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 3. Gọi hàm gửi yêu cầu thu hồi
                await _chatService.SendRecallRequestAsync(messageToRecall.MessageId);

                // (Tùy chọn) Bỏ chọn tin nhắn sau khi gửi yêu cầu
                ChatListBox.SelectedItem = null;
            }
            else
            {
                // Trường hợp người dùng chưa chọn tin nhắn
                MessageBox.Show("Vui lòng chọn một tin nhắn để thu hồi.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}