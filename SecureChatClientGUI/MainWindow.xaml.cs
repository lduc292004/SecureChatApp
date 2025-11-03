using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // Cần thêm cho ToggleButton

namespace SecureChatClientGUI
{
    public partial class MainWindow : Window
    {
        // Khởi tạo ChatClientService ở đây (Sẽ được gán giá trị khi nhấn Connect)
        private ChatClientService? _chatService;

        // BIẾN LƯU TRẠNG THÁI THEME
        private bool _isDarkMode = false;

        public MainWindow()
        {
            InitializeComponent();
            this.Closing += MainWindow_Closing;
            ChatListBox.Loaded += ChatListBox_Loaded;

            // Khởi tạo trạng thái điều khiển ban đầu
            UpdateControlStates();
        }

        // 
        // ====================================================================
        // HÀM MỚI: XỬ LÝ NÚT GẠT CHUYỂN DARK/LIGHT MODE
        // ====================================================================
        //
        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // Đảo ngược trạng thái
            _isDarkMode = !_isDarkMode;

            // Gọi hàm static trong App.xaml.cs
            App.SwitchTheme(_isDarkMode);

            // Cập nhật trạng thái IsChecked của nút (nếu bạn muốn)
            if (sender is ToggleButton toggleButton)
            {
                toggleButton.IsChecked = _isDarkMode;
            }
        }

        // 
        // ====================================================================
        // CÁC HÀM CŨ CỦA BẠN (GIỮ NGUYÊN)
        // ====================================================================
        //

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
            openFileDialog.Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                await _chatService.SendImageAsync(filePath);
                ScrollToBottom();
            }
        }

        // Hàm cuộn xuống cuối cùng
        private void ScrollToBottom()
        {
            Dispatcher.Invoke(() =>
            {
                if (ChatListBox.Items.Count > 0)
                {
                    ChatListBox.ScrollIntoView(ChatListBox.Items[ChatListBox.Items.Count - 1]);
                }
            });
        }

        // Sự kiện tự động cuộn khi ListBox được tải
        private void ChatListBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
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

            Dispatcher.Invoke(() =>
            {
                ConnectButton.IsEnabled = !isConnected;
                DisconnectButton.IsEnabled = isConnected;
                SendButton.IsEnabled = isConnected;
                CreateGroupButton.IsEnabled = isConnected;
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

        private async void CreateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_chatService == null || !_chatService.IsConnected)
            {
                MessageBox.Show("Chưa kết nối đến máy chủ.", "Lỗi Tạo Nhóm", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new CreateGroupWindow();
            if (dlg.ShowDialog() == true)
            {
                string groupName = dlg.GroupName?.Trim() ?? string.Empty;
                string membersCsv = dlg.MembersCsv?.Trim() ?? string.Empty;
                var members = membersCsv.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

                await _chatService.SendCreateGroupAsync(groupName, members);
            }
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

            if (ChatListBox.SelectedItem is ChatMessage messageToRecall)
            {
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

                await _chatService.SendRecallRequestAsync(messageToRecall.MessageId);
                ChatListBox.SelectedItem = null;
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một tin nhắn để thu hồi.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}