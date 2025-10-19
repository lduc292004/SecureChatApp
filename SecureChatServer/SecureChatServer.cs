using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Security.Authentication;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using Serilog;

public class SecureChatServer
{
    private const int Port = 5000;
    private const string CertPath = "D:\\server.pfx";
    private const string CertPassword = "password123";

    // Key: SslStream, Value: Ten nguoi dung (string)
    private static ConcurrentDictionary<SslStream, string> clientStreams = new ConcurrentDictionary<SslStream, string>();

    // DANH SACH CAC MAU REGEX BI CAM (Phase 7)
    private static readonly List<string> BlacklistPatterns = new List<string>
    {
        // URL/Link
        @"(\bhttps?:\/\/(?:www\.|(?!www))[a-zA-Z0-9][a-zA-Z0-9-]+[a-zA-Z0-9]\.[^\s]{2,}|www\.[a-zA-Z0-9][a-zA-Z0-9-]+[a-zA-Z0-9]\.[^\s]{2,}|[a-zA-Z0-9]+\.[^\s]{2,})",
        // Số điện thoại
        @"\b(\+84|0)(3|5|7|8|9)\d{8}\b", 
        // Lời mời nhập thông tin cá nhân/tài khoản
        @"\b(nhap\s+mat\s+khau|doi\s+the\s+cao|gui\s+thong\s+tin\s+tai\s+khoan|dang\s+nhap\s+ngay)\b", 
        // Lời mời gọi hành động đáng ngờ (Phishing)
        @"\b(click\s+vao\s+link|truy\s+cap\s+gap|xac\s+minh\s+ngay)\b"
    };

    public static async Task StartServer()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, Port);

        try
        {
            listener.Start();
            Log.Information("Server da khoi dong SECURE tai: {IpAddress}:{Port}", IPAddress.Any, Port);
            Console.WriteLine("Dang cho ket noi...");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                // Khởi chạy xử lý client trong một Task riêng biệt
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Loi Server: {Message}", ex.Message);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task BroadcastMessageAsync(string senderName, string message)
    {
        bool isSystemMessage = senderName == "SERVER";

        // Định dạng tin nhắn: [HH:mm] Tên: Nội dung (trừ tin nhắn SERVER)
        string formattedMessage = isSystemMessage ? message : $"[{DateTime.Now.ToShortTimeString()}] {senderName}: {message}";

        Log.Information("[BROADCAST] {Message}", formattedMessage);

        // Đảm bảo tin nhắn kết thúc bằng \n
        byte[] buffer = Encoding.UTF8.GetBytes(formattedMessage + "\n");

        foreach (var clientEntry in clientStreams)
        {
            // Chỉ loại trừ người gửi nếu đó không phải tin nhắn SERVER
            if (isSystemMessage || clientEntry.Value != senderName)
            {
                try
                {
                    // Ghi dữ liệu vào SslStream
                    await clientEntry.Key.WriteAsync(buffer, 0, buffer.Length);
                    await clientEntry.Key.FlushAsync(); // Đẩy dữ liệu ra ngay lập tức
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Loi khi gui broadcast toi client {ClientName}. Ngat ket noi.", clientEntry.Value);
                    // Nếu lỗi, loại bỏ client khỏi danh sách
                    clientStreams.TryRemove(clientEntry.Key, out _);
                }
            }
        }
    }

    // ************ HÀM TIỆN ÍCH: ĐỌC DỮ LIỆU TIN CẬY THEO DÒNG ************
    private static async Task<string?> ReadMessageLineAsync(SslStream stream, CancellationToken token = default)
    {
        var buffer = new byte[1];
        var message = new StringBuilder();

        while (true)
        {
            // Đọc từng byte một cho đến khi tìm thấy ký tự \n
            int bytesRead;
            try
            {
                bytesRead = await stream.ReadAsync(buffer, 0, 1, token);
            }
            catch (System.ObjectDisposedException)
            {
                return null; // Stream đã bị đóng
            }
            catch (System.IO.IOException)
            {
                return null; // Client đã ngắt kết nối
            }


            if (bytesRead == 0)
            {
                // Kết thúc Stream (Client đóng kết nối)
                return message.Length > 0 ? message.ToString() : null;
            }

            char c = Encoding.UTF8.GetChars(buffer)[0];

            if (c == '\n')
            {
                // Kết thúc tin nhắn
                return message.ToString().TrimEnd('\r'); // Xóa ký tự xuống dòng của Windows (\r)
            }

            message.Append(c);
        }
    }

    private static bool ContainsBlacklistedContent(string message)
    {
        string lowerCaseMessage = message.ToLowerInvariant();
        foreach (var pattern in BlacklistPatterns)
        {
            if (Regex.IsMatch(lowerCaseMessage, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return true;
            }
        }
        return false;
    }

    private static async Task ReceiveFileAsync(SslStream stream, string fileName, long fileSize)
    {
        const string uploadDir = "TransferredFiles";
        Directory.CreateDirectory(uploadDir);

        string savePath = Path.Combine(uploadDir, fileName);

        Log.Information("[FILE] Dang nhan file: {FileName} ({FileSize} bytes) -> {SavePath}", fileName, fileSize, savePath);

        try
        {
            using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                byte[] buffer = new byte[8192];
                long bytesReceived = 0;
                int bytesRead;

                while (bytesReceived < fileSize && (bytesRead = await stream.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, fileSize - bytesReceived))) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    bytesReceived += bytesRead;

                    if (bytesReceived % (1024 * 1024) == 0 || bytesReceived == fileSize)
                    {
                        Log.Debug("[FILE] Tien trinh: {Progress}%", bytesReceived * 100 / fileSize);
                    }
                }
            }
            Log.Information("[FILE] Hoan tat nhan file: {FileName}.", fileName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[FILE ERROR] Loi khi nhan file {FileName}: {Message}", fileName, ex.Message);
        }
    }

    // ************ PHƯƠNG THỨC XỬ LÝ CLIENT ĐÃ SỬA LỖI ĐỌC STREAM ************
    private static async Task HandleClientAsync(TcpClient client)
    {
        X509Certificate2 serverCertificate;
        try
        {
            if (!File.Exists(CertPath))
            {
                Log.Fatal("Khong tim thay file chung chi tai duong dan: {Path}", CertPath);
                client.Close();
                return;
            }

            serverCertificate = new X509Certificate2(
                CertPath,
                CertPassword,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet
            );

            Log.Information("Da tai chung chi thanh cong tu: {Path}", CertPath);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Loi tai chung chi: {Message}", ex.Message);
            client.Close();
            return;
        }

        NetworkStream netStream = client.GetStream();
        SslStream sslStream = new SslStream(netStream, false);
        string currentName = "Client_Unidentified";

        try
        {
            // 1. AUTHENTICATION (Xác thực SSL/TLS)
            await sslStream.AuthenticateAsServerAsync(serverCertificate, clientCertificateRequired: false,
                                                     enabledSslProtocols: SslProtocols.Tls12 | SslProtocols.Tls13,
                                                     checkCertificateRevocation: true);

            Log.Information("Client: {RemoteEndPoint} da hoan tat bat tay SSL/TLS.", client.Client.RemoteEndPoint);

            clientStreams.TryAdd(sslStream, currentName);

            // 2. VÒNG LẶP ĐỌC DỮ LIỆU (SỬ DỤNG HÀM ĐỌC DÒNG TÙY CHỈNH)
            string? data;
            while ((data = await ReadMessageLineAsync(sslStream)) != null)
            {
                data = data.Trim();
                if (string.IsNullOrEmpty(data)) continue;

                // KIEM TRA LENH DOI TEN / CAI DAT TEN
                if (data.StartsWith("[SET_NAME]:"))
                {
                    string newName = data.Substring("[SET_NAME]:".Length).Trim();
                    if (string.IsNullOrWhiteSpace(newName)) continue;

                    if (currentName != "Client_Unidentified")
                    {
                        await BroadcastMessageAsync("SERVER", $"{currentName} da doi ten thanh {newName}.");
                    }
                    else
                    {
                        await BroadcastMessageAsync("SERVER", $"{newName} da tham gia phong chat.");
                    }

                    // Cập nhật tên
                    clientStreams.TryUpdate(sslStream, newName, currentName);
                    currentName = newName;

                    Log.Information("Client {RemoteEndPoint} da doi ten thanh {ClientName}", client.Client.RemoteEndPoint, currentName);
                    continue;
                }

                // KIEM TRA GIAO THUC TRUYEN TEP
                if (data.StartsWith("[FILE_START]:"))
                {
                    string metadata = data.Substring("[FILE_START]:".Length);
                    string[] parts = metadata.Split('|');

                    if (parts.Length == 2 && long.TryParse(parts[1], out long fileSize))
                    {
                        string fileName = parts[0];
                        await BroadcastMessageAsync(currentName, $"bat dau gui file: {fileName} ({fileSize} bytes).");

                        await ReceiveFileAsync(sslStream, fileName, fileSize);

                        await BroadcastMessageAsync(currentName, $"da gui file: {fileName} thanh cong.");
                    }
                    else
                    {
                        Log.Error("[ERROR] Metadata file khong hop le: {Data}", data);
                    }
                }
                else
                {
                    // TIN NHAN CHAT THONG THUONG - KIEM TRA NOI DUNG DOC HAI
                    if (ContainsBlacklistedContent(data))
                    {
                        Log.Warning("BLOCKED: Tin nhan tu {ClientName} bi chan do chua noi dung bi cam. Noi dung: {Data}", currentName, data);

                        string warning = "[SERVER WARNING]: Tin nhan cua ban bi chan vi co chua noi dung bi cam.\n";
                        byte[] warningBuffer = Encoding.UTF8.GetBytes(warning);
                        await sslStream.WriteAsync(warningBuffer, 0, warningBuffer.Length);
                        await sslStream.FlushAsync();
                    }
                    else
                    {
                        await BroadcastMessageAsync(currentName, data);
                    }
                }
            }
        }
        catch (AuthenticationException ex)
        {
            Log.Error(ex, "Loi Xac thuc SSL/TLS cho client {ClientName}: {Message}", currentName, ex.Message);
        }
        catch (IOException)
        {
            // Lỗi khi Client đóng kết nối hoặc mạng bị ngắt
            Log.Information("Client ({RemoteEndPoint}) da ngat ket noi.", client.Client.RemoteEndPoint);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Loi xu ly Client ({RemoteEndPoint}): {Message}", client.Client.RemoteEndPoint, ex.Message);
        }
        finally
        {
            clientStreams.TryRemove(sslStream, out _);
            if (currentName != "Client_Unidentified")
            {
                await BroadcastMessageAsync("SERVER", $"{currentName} da roi phong chat.");
            }

            sslStream?.Close();
            client.Close();
        }
    }
}

// ************ ĐÃ SỬA: ĐỔI TÊN LỚP TỪ SecureChatClient thành Program ************
public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("server.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("Server dang khoi dong...");
            // Gọi phương thức StartServer từ lớp SecureChatServer
            await SecureChatServer.StartServer();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Server da bi dung do loi khong mong muon.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
