using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Serilog;

public class SecureChatServer
{
    private const int Port = 5000;
    private const string CertPath = "securechat.pfx";
    private const string CertPassword = "password123";
    private const string UploadDir = "ServerUploads";

    private static readonly ConcurrentDictionary<string, ClientConnection> _clients = new();

    //Them luu tru tin nhhat
    private static readonly ConcurrentDictionary<string, ChatMessageRecord> _messageHistory = new();
    public static async Task StartServer()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("server.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            if (!File.Exists(CertPath))
            {
                Console.WriteLine($"❌ Không tìm thấy chứng chỉ: {CertPath}");
                return;
            }

            Directory.CreateDirectory(UploadDir);

            var cert = new X509Certificate2(CertPath, CertPassword, X509KeyStorageFlags.MachineKeySet);
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            Log.Information("🚀 Server đang chạy trên cổng {Port}", Port);

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client, cert);
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Lỗi khi khởi động server: {Message}", ex.Message);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static async Task HandleClientAsync(TcpClient client, X509Certificate2 cert)
    {
        string clientId = Guid.NewGuid().ToString();
        string clientName = "Guest_" + clientId[..4];
        SslStream? ssl = null;

        try
        {
            ssl = new SslStream(client.GetStream(), false);
            await ssl.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);

            var conn = new ClientConnection(clientId, clientName, ssl, client);
            _clients.TryAdd(clientId, conn);

            Console.WriteLine($"✅ {clientName} đã kết nối.");
            await BroadcastMessageAsync($"[INFO] {clientName} đã tham gia phòng chat.", conn);

            await ReceiveLoopAsync(conn);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Lỗi client {Name}: {Message}", clientName, ex.Message);
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            client.Close();
            ssl?.Close();
            await BroadcastMessageAsync($"[INFO] {clientName} đã ngắt kết nối.", null);
        }
    }

    private static async Task ReceiveLoopAsync(ClientConnection clientConn)
    {
        var stream = clientConn.Stream!;
        var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            string? msg;
            try
            {
                msg = await reader.ReadLineAsync();
                if (msg == null) break;

                if (msg.StartsWith("[SET_NAME]:"))
                {
                    await HandleSetName(clientConn, msg);
                }
                else if (msg.StartsWith("[IMG_START]:"))
                {
                    await HandleIncomingImage(clientConn, msg);
                
                }
                //  Xử lý yêu cầu thu hoi
                else if (msg.StartsWith("[RECALL_REQ]:"))
                {
                    await HandleRecallRequestAsync(clientConn, msg);
                }
                //   Xử lý tin nhắn chat có MessageID (Client Console gửi)
                else if (msg.StartsWith("[MSG]:"))
                {
                    await HandleChatMessageAsync(clientConn, msg);
                }
                // ⭐ PHẦN ĐÃ SỬA: Bỏ kiểm tra [MSG]:. Mọi tin nhắn không phải lệnh đều được coi là tin nhắn chat.
                else
                {
                    await BroadcastMessageAsync($"[{clientConn.Name}]: {msg}", clientConn);
                }
            }
            catch (IOException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Lỗi khi nhận dữ liệu từ {Name}", clientConn.Name);
                break;
            }
        }
    }
    private static async Task HandleChatMessageAsync(ClientConnection sender, string message)
    {
        // Định dạng Client gửi: [MSG]:<MessageId>|<Sender>|<Content>
        try
        {
            string data = message.Substring("[MSG]:".Length);
            var parts = data.Split(new[] { '|' }, 3);

            if (parts.Length == 3)
            {
                string msgId = parts[0];
                string senderName = parts[1];
                string content = parts[2];

                // 1. LƯU TRỮ tin nhắn
                var record = new ChatMessageRecord
                {
                    MessageId = msgId,
                    SenderId = sender.Id, // Lưu ID để xác thực thu hồi
                    SenderName = sender.Name,
                    Content = content
                };
                _messageHistory.TryAdd(msgId, record);

                // 2. BROADCAST lại cho tất cả (format đơn giản: [Sender]: Content)
                string broadcastMsg = $"[MSG_BROADCAST]:{msgId}|{sender.Name}|{content}";
                await BroadcastMessageAsync(broadcastMsg, null); // Gửi cho tất cả (bao gồm người gửi)

                Console.WriteLine($"💬 Nhận tin: {sender.Name} (ID: {msgId}): {content}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Lỗi khi xử lý tin nhắn chat từ {Name}", sender.Name);
        }
    }
    // ⭐ HÀM MỚI: Xử lý Yêu cầu Thu hồi
    private static async Task HandleRecallRequestAsync(ClientConnection requester, string message)
    {
        // Định dạng Client gửi: [RECALL_REQ]:<MessageId>
        string msgIdToRecall = message.Substring("[RECALL_REQ]:".Length).Trim();

        if (string.IsNullOrWhiteSpace(msgIdToRecall)) return;

        // 1. TÌM và KIỂM TRA quyền thu hồi
        if (_messageHistory.TryGetValue(msgIdToRecall, out var record))
        {
            // Chỉ người gửi gốc mới có quyền thu hồi
            if (record.SenderId == requester.Id)
            {
                // 2. XÓA khỏi lịch sử Server
                if (_messageHistory.TryRemove(msgIdToRecall, out _))
                {
                    // 3. BROADCAST lệnh thu hồi cho tất cả Clients
                    string recallCommand = $"[RECALL]:{msgIdToRecall}";
                    await BroadcastMessageAsync(recallCommand, null); // Gửi cho TẤT CẢ clients

                    Console.WriteLine($"✅ Thu hồi thành công. MessageId: {msgIdToRecall} (Người gửi: {requester.Name})");
                }
            }
            else
            {
                Log.Warning("⚠️ {Name} cố gắng thu hồi tin nhắn của người khác (ID: {MsgId})", requester.Name, msgIdToRecall);
            }
        }
        else
        {
            Console.WriteLine($"[WARN] Yêu cầu thu hồi MessageId không tồn tại: {msgIdToRecall}");
        }
    }
    private static async Task HandleIncomingImage(ClientConnection sender, string header)
    {
        // [IMG_START]:filename|size|mime
        string[] parts = header.Substring(12).Split('|');
        if (parts.Length < 3) return;

        string fileName = parts[0];
        long fileSize = long.Parse(parts[1]);
        string mime = parts[2];
        string savePath = Path.Combine(UploadDir, GetUniqueFileName(fileName));

        Console.WriteLine($"🖼️ Đang nhận ảnh {fileName} ({fileSize} bytes) từ {sender.Name}");

        try
        {
            byte[] buffer = new byte[8192];
            long total = 0;

            using (var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write))
            {
                while (total < fileSize)
                {
                    int read = await sender.Stream!.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, fileSize - total));
                    if (read <= 0) break;

                    await fs.WriteAsync(buffer, 0, read);
                    total += read;
                }
            }

            Console.WriteLine($"✅ Đã nhận ảnh {fileName} từ {sender.Name}. Gửi lại cho các client khác...");

            byte[] data = File.ReadAllBytes(savePath);
            await BroadcastImageAsync(sender, fileName, data, mime);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Lỗi khi nhận ảnh từ {Name}", sender.Name);
        }
    }

    private static async Task BroadcastImageAsync(ClientConnection sender, string fileName, byte[] data, string mime)
    {
        string header = $"[IMG_BROADCAST]:{fileName}|{data.Length}|{mime}\n";
        byte[] headerBytes = Encoding.UTF8.GetBytes(header);

        foreach (var client in _clients.Values)
        {
            if (client.Id == sender.Id) continue;

            try
            {
                await client.Stream!.WriteAsync(headerBytes, 0, headerBytes.Length);
                await client.Stream.WriteAsync(data, 0, data.Length);
                await client.Stream.WriteAsync(Encoding.UTF8.GetBytes("\n[IMG_END]\n"));
                await client.Stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠️ Không gửi được ảnh cho {Name}", client.Name);
            }
        }
    }

    private static async Task HandleSetName(ClientConnection client, string msg)
    {
        string name = msg.Substring(11).Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            string old = client.Name;
            client.Name = name;
            await BroadcastMessageAsync($"[INFO] {old} đổi tên thành {name}.", null);
        }
    }

    private static async Task BroadcastMessageAsync(string msg, ClientConnection? sender)
    {
        // Kiểm tra xem tin nhắn/lệnh đã kết thúc bằng ký tự xuống dòng chưa.
        // Nếu tin nhắn đã là một lệnh (ví dụ: [RECALL]:id), ta không thêm \n
        // Nếu là tin nhắn chat thông thường, ta đảm bảo có \n để Client Reader kết thúc.
        string finalMsg = msg;
        if (!msg.EndsWith('\n'))
        {
            finalMsg += "\n";
        }

        byte[] bytes = Encoding.UTF8.GetBytes(finalMsg);

        foreach (var client in _clients.Values)
        {
            // Bỏ qua người gửi (trừ khi sender là null, tức là broadcast cho tất cả)
           

            try
            {
                await client.Stream!.WriteAsync(bytes, 0, bytes.Length);
                await client.Stream.FlushAsync();
            }
            catch
            {
                // Bỏ qua client bị lỗi (có thể đã ngắt kết nối)
            }
        }
    }

    private static string GetUniqueFileName(string name)
    {
        string baseName = Path.GetFileNameWithoutExtension(name);
        string ext = Path.GetExtension(name);
        string result = name;
        int i = 1;

        while (File.Exists(Path.Combine(UploadDir, result)))
        {
            result = $"{baseName}({i++}){ext}";
        }

        return result;
    }
}
// ⭐THÊM: Class lưu trữ chi tiết tin nhắn trên Server
public class ChatMessageRecord
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string SenderId { get; set; } // ID duy nhất của người gửi
    public string SenderName { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class ClientConnection
{
    public string Id { get; }
    public string Name { get; set; }
    public SslStream? Stream { get; }
    public TcpClient? Client { get; }

    public ClientConnection(string id, string name, SslStream stream, TcpClient client)
    {
        Id = id;
        Name = name;
        Stream = stream;
        Client = client;
    }
}

public class Program
{
    public static async Task Main()
    {
        await SecureChatServer.StartServer();
    }
}