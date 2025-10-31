// File: MainWindow.xaml.cs (Phiên bản đã tối ưu hóa)
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System;
using System.Linq; // Cần thiết cho các thao tác LINQ trên Collections

namespace SecureChatClientGUI
{
    public partial class MainWindow : Window
    {
        // ⭐ 1. KHỞI TẠO CHAT SERVICE NGAY LẬP TỨC
        // Bây giờ _chatService là DataContext, KHÔNG phải thuộc tính của MainWindow
        private ChatClientService _chatService;

        public MainWindow()
        {
            // TẠO SERVICE TRƯỚC HẾT
            _chatService = new ChatClientService("UI_Test_User");

            // ⭐ 2. GÁN DataContext TRƯỚC InitializeComponent()
            this.DataContext = _chatService;

            InitializeComponent();

            this.Closing += MainWindow_Closing;

            // ⭐ 3. ĐĂNG KÝ SỰ KIỆN TỰ ĐỘNG CUỘN
            // Messages đã được khởi tạo, đăng ký ngay lập tức.
            _chatService.Messages.CollectionChanged += (s, args) =>
            {
                if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    // Đảm bảo cuộn được gọi trên UI thread
                    ScrollToBottom();
                }
            };

            // ⭐ 4. NẠP DỮ LIỆU MẪU ĐỂ TEST GIAO DIỆN
            LoadSampleMessages();

            // Đăng ký StatusChanged (đã làm trong Connect, có thể lặp lại ở đây)
            _chatService.StatusChanged += (status) => Dispatcher.Invoke(() => StatusTextBlock.Text = $"Trạng thái: {status}");

            UpdateControlStates();
        }

        private void LoadSampleMessages()
        {
            // Nạp dữ liệu vào Messages của Service (Đã là DataContext)
            _chatService.Messages.Add(new ChatMessage { Content = "Chào bạn, mình test giao diện Chat Bubble mới nhé!", IsMine = true, Timestamp = DateTime.Now.AddMinutes(-5) });
            _chatService.Messages.Add(new ChatMessage { Content = "Tuyệt vời! Giao diện này đẹp hơn hẳn đó.", IsMine = false, Sender = "Bạn A", Timestamp = DateTime.Now.AddMinutes(-3) });
            _chatService.Messages.Add(new ChatMessage { Content = "Đây là tin nhắn của mình, nó sẽ căn phải.", IsMine = true, Timestamp = DateTime.Now.AddMinutes(-2) });
            _chatService.Messages.Add(new ChatMessage { Content = "Và đây là tin nhắn của người khác, nó sẽ căn trái.", IsMine = false, Sender = "Bạn B", Timestamp = DateTime.Now.AddMinutes(-1) });
            // Thêm tin nhắn thu hồi (để test logic IsRecalled)
            _chatService.Messages.Add(new ChatMessage { Content = "Tin nhắn này bị thu hồi để kiểm tra UI.", IsMine = true, IsRecalled = true, Timestamp = DateTime.Now });

            // ⭐ Nếu bạn có ảnh để test, thay thế placeholder này bằng code tải ảnh thực tế.
            // Ví dụ: _chatService.Messages.Add(new ChatMessage { Image = new BitmapImage(new Uri("pack://application:,,,/Resources/sample.jpg")), IsMine = false });
        }

        // ... (Giữ nguyên logic của các hàm khác, nhưng KHÔNG GÁN LẠI DataContext trong ConnectButton_Click) ...

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _chatService.Disconnect(); // Gọi Disconnect trên đối tượng đã khởi tạo
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            string userName = UserNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(userName)) return;

            // ⭐ KHÔNG CẦN TẠO MỚI _chatService nữa (vì đã tạo ở Constructor)
            // Nếu bạn muốn đổi tên, chỉ cần gọi hàm Connect trên Service hiện tại

            // Cố gắng kết nối
            bool connected = await _chatService.ConnectAsync();
            UpdateControlStates();

            if (connected)
            {
                string setNameCommand = $"[SET_NAME]:{userName}";
                await _chatService.SendMessageAsync(setNameCommand);
            }
        }

        // Các hàm khác giữ nguyên: DisconnectButton_Click, SendButton_Click, MessageTextBox_KeyDown, SendFileButton_Click, ScrollToBottom, UpdateControlStates, OnlineUsersListBox_SelectionChanged, btnThuHoi_Click

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            _chatService.Disconnect();
            UpdateControlStates();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_chatService.IsConnected) return;

            string message = MessageTextBox.Text;
            if (string.IsNullOrWhiteSpace(message)) return;

            await _chatService.SendMessageAsync(message);
            MessageTextBox.Clear();
        }

        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private async void SendFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_chatService.IsConnected) return;

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                await _chatService.SendImageAsync(filePath);
            }
        }

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

        private void UpdateControlStates()
        {
            bool isConnected = _chatService?.IsConnected ?? false;

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

        private void OnlineUsersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Thêm logic xử lý khi người dùng chọn một user trong danh sách
        }

        private async void btnThuHoi_Click(object sender, RoutedEventArgs e)
        {
            if (!_chatService.IsConnected)
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