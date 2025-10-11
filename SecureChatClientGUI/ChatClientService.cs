using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
// Dòng này được xóa vì không dùng và gây lỗi CS1955:
// using static System.Net.Mime.MediaTypeNames; 

namespace SecureChatClientGUI
{
    // Lớp ChatClientService
    public class ChatClientService
    {
        private const string ServerIP = "127.0.0.1";
        private const int Port = 5000;
        private const string ServerName = "SecureChatServer";

        private TcpClient? _client;
        private SslStream? _sslStream;
        private string _userName;

        public bool IsConnected { get; private set; }

        public event Action<string>? StatusChanged;

        // ********** THUỘC TÍNH CHO BINDING: Dùng ChatMessage **********
        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();
        public ObservableCollection<string> OnlineUsers { get; } = new ObservableCollection<string>();

        public ChatClientService(string userName)
        {
            _userName = userName;
        }

        // Hàm kiểm tra chứng chỉ (Giữ nguyên)
        public static bool ValidateServerCertificate(
                      object sender,
                      X509Certificate certificate,
                      X509Chain chain,
                      SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        // ********** KẾT NỐI **********
        public async Task ConnectAsync()
        {
            // ... (Giữ nguyên)
            try
            {
                StatusChanged?.Invoke("Dang ket noi...");
                _client = new TcpClient();
                await _client.ConnectAsync(ServerIP, Port);

                _sslStream = new SslStream(
                    _client.GetStream(),
                    false,
                    new RemoteCertificateValidationCallback(ValidateServerCertificate)
                );

                await _sslStream.AuthenticateAsClientAsync(ServerName);
                IsConnected = true;
                StatusChanged?.Invoke("Da ket noi SECURE thanh cong!");

                // Gửi tên người dùng
                byte[] nameBuffer = Encoding.UTF8.GetBytes($"[SET_NAME]:{_userName}\n");
                await _sslStream.WriteAsync(nameBuffer, 0, nameBuffer.Length);

                // Bắt đầu vòng lặp lắng nghe
                Task.Run(ReceiveLoop);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Loi ket noi: {ex.Message}");
                Disconnect();
            }
        }

        // ********** GỬI TIN NHẮN **********
        public async Task SendMessageAsync(string message)
        {
            // ... (Giữ nguyên)
            if (!IsConnected || _sslStream == null) return;

            try
            {
                // Thêm tin nhắn của chính mình vào list ngay lập tức (IsMine = True)
                Messages.Add(new ChatMessage { Content = message, Sender = _userName, IsMine = true });

                // Gửi tin nhắn
                byte[] data = Encoding.UTF8.GetBytes(message + "\n");
                await _sslStream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Loi gui tin: {ex.Message}");
                Disconnect();
            }
        }

        // ********** VÒNG LẶP LẮNG NGHE (SỬ DỤNG STREAMREADER ĐỂ ĐỌC DÒNG) **********
        private async Task ReceiveLoop()
        {
            if (_sslStream == null) return;

            using (var reader = new StreamReader(_sslStream, Encoding.UTF8))
            {
                try
                {
                    string? rawMessage;

                    while (IsConnected && (rawMessage = await reader.ReadLineAsync()) != null)
                    {
                        string message = rawMessage.Trim();

                        if (string.IsNullOrEmpty(message)) continue;

                        // ** PHÂN TÍCH TIN NHẮN TỪ SERVER **
                        string sender = "Server";
                        string content = message;

                        if (message.StartsWith('['))
                        {
                            int endBracket = message.IndexOf(']');
                            if (endBracket > 0 && endBracket < message.Length - 1)
                            {
                                string part = message.Substring(endBracket + 1).TrimStart();

                                int colon = part.IndexOf(':');
                                if (colon > 0)
                                {
                                    // Trường hợp: "SenderName: Content"
                                    sender = part.Substring(0, colon).Trim();
                                    content = part.Substring(colon + 1).Trim();
                                }
                                else
                                {
                                    // Trường hợp: "[BROADCAST] Welcome!"
                                    content = part;
                                    sender = "Hệ thống";
                                }
                            }
                        }

                        // DO LỖI CS1955 NẰM Ở ĐÂY -> ĐÃ SỬA DỤNG Dispatcher.Invoke
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Chỉ thêm tin nhắn của người khác hoặc tin nhắn hệ thống
                            // Loại bỏ tin nhắn của chính mình vì đã thêm trong SendMessageAsync
                            if (!sender.Equals(_userName, StringComparison.OrdinalIgnoreCase))
                            {
                                Messages.Add(new ChatMessage { Content = content, Sender = sender, IsMine = false });
                            }
                            // Thêm logic cập nhật OnlineUsers ở đây nếu Server gửi danh sách
                        });
                    }
                }
                catch (IOException)
                {
                    // Lỗi đọc/ghi, có thể do Server đóng kết nối
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke($"Loi lang nghe: {ex.Message}");
                }
            }
            Disconnect();
        }

        // ********** NGẮT KẾT NỐI **********
        public void Disconnect()
        {
            // ... (Giữ nguyên)
            if (!IsConnected) return;

            IsConnected = false;
            try
            {
                _sslStream?.Close();
                _client?.Close();
            }
            catch { /* Bo qua loi khi dong */ }

            StatusChanged?.Invoke("Da ngat ket noi.");
        }
    }
}