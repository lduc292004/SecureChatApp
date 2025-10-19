using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Security.Authentication;
using System.IO;

public class SecureChatClient
{
    private const string ServerIP = "127.0.0.1";
    private const int Port = 5000;
    private const string ServerName = "SecureChatServer";

    private static string _currentClientName = "";

    // ************ HÀM TIỆN ÍCH MỚI: ĐỌC DỮ LIỆU TIN CẬY THEO DÒNG ************
    // Hàm này đảm bảo đọc trọn vẹn một dòng tin nhắn, ngay cả khi nó bị chia nhỏ qua mạng.
    private static async Task<string?> ReadMessageLineAsync(SslStream stream, CancellationToken token = default)
    {
        var buffer = new byte[1];
        var message = new StringBuilder();

        while (true)
        {
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
                return null; // Server đã ngắt kết nối
            }
            catch (System.OperationCanceledException)
            {
                return null; // Hủy bỏ
            }


            if (bytesRead == 0)
            {
                // Kết thúc Stream (Server đóng kết nối)
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
    // *************************************************************************

    public static async Task SendFileAsync(SslStream stream, string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[FILE ERROR] Khong tim thay file tai: {filePath}");
            return;
        }

        FileInfo fileInfo = new FileInfo(filePath);
        string fileName = fileInfo.Name;
        long fileSize = fileInfo.Length;

        Console.WriteLine($"[FILE] Dang chuan bi gui file: {fileName} ({fileSize} bytes)");

        string metadata = $"[FILE_START]:{fileName}|{fileSize}\n";
        byte[] metadataBuffer = Encoding.UTF8.GetBytes(metadata);
        await stream.WriteAsync(metadataBuffer, 0, metadataBuffer.Length);

        try
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            {
                byte[] buffer = new byte[8192];
                int bytesRead;
                long bytesSent = 0;

                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, bytesRead);
                    bytesSent += bytesRead;

                    if (bytesSent % (1024 * 1024) == 0 || bytesSent == fileSize)
                    {
                        Console.WriteLine($"[FILE] Tien trinh: {bytesSent * 100 / fileSize}%");
                    }
                }
            }
            Console.WriteLine($"[FILE] Hoan tat gui file: {fileName}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FILE ERROR] Loi khi gui file {fileName}: {ex.Message}");
        }
    }

    public static async Task StartClient()
    {
        // ** YEU CAU NHAP TEN **
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("Vui long nhap ten cua ban: ");
        _currentClientName = Console.ReadLine() ?? "Anonymous";
        Console.ResetColor();

        int maxRetries = 5;
        int retryCount = 0;

        // ** VONG LAP TAI KET NOI **
        while (retryCount < maxRetries)
        {
            TcpClient client = null;
            SslStream sslStream = null;

            try
            {
                client = new TcpClient();
                Console.WriteLine($"Dang co gang ket noi SECURE toi Server ({retryCount + 1}/{maxRetries})...");
                await client.ConnectAsync(ServerIP, Port);

                NetworkStream netStream = client.GetStream();
                sslStream = new SslStream(netStream, false, new RemoteCertificateValidationCallback(ValidateServerCertificate));

                await sslStream.AuthenticateAsClientAsync(ServerName);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✅ Da ket noi SECURE toi Server thanh cong!");
                Console.ResetColor();

                // Gui ten sau khi ket noi thanh cong
                byte[] nameBuffer = Encoding.UTF8.GetBytes($"[SET_NAME]:{_currentClientName}\n");
                await sslStream.WriteAsync(nameBuffer, 0, nameBuffer.Length);

                // Start listening with the reliable method
                _ = Task.Run(() => ReceiveMessagesAsync(sslStream));

                // Vong lap chat chinh
                while (true)
                {
                    Console.Write($"{_currentClientName}: ");
                    string message = Console.ReadLine() ?? "";

                    if (message.ToLower() == "exit" || message.ToLower() == "quit")
                    {
                        Console.WriteLine("Dang dong ket noi...");
                        return; // Thoat khoi ham StartClient
                    }

                    // KIEM TRA LENH DOI TEN
                    if (message.StartsWith("/name "))
                    {
                        string newName = message.Substring("/name ".Length).Trim();
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            byte[] renameBuffer = Encoding.UTF8.GetBytes($"[SET_NAME]:{newName}\n");
                            await sslStream.WriteAsync(renameBuffer, 0, renameBuffer.Length);
                            _currentClientName = newName;
                            Console.WriteLine($"[INFO] Ten cua ban da duoc cap nhat thanh: {_currentClientName}");
                        }
                        continue;
                    }

                    // KIEM TRA LENH GUI FILE
                    if (message.StartsWith("/sendfile "))
                    {
                        string filePath = message.Substring("/sendfile ".Length).Trim().Trim('"');
                        await SendFileAsync(sslStream, filePath);
                        continue;
                    }

                    // GUI TIN NHAN CHAT THONG THUONG
                    byte[] data = Encoding.UTF8.GetBytes(message + "\n");
                    await sslStream.WriteAsync(data, 0, data.Length);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Loi: Khong the ket noi. Server dang OFF.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Loi ket noi hoac giao thuc: {ex.Message}");
                Console.ResetColor();
            }
            finally
            {
                sslStream?.Close();
                client?.Close();
            }

            // TAI KET NOI
            retryCount++;
            if (retryCount < maxRetries)
            {
                int delaySeconds = 5;
                Console.WriteLine($"\n[RETRY] Dang thu ket noi lai sau {delaySeconds} giay...");
                await Task.Delay(delaySeconds * 1000);
            }
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[EXIT] Da that bai {maxRetries} lan. Client dong.");
        Console.ResetColor();
    }

    // Ham chap nhan chung chi tu ky (Giữ nguyên)
    public static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[CANH BAO BAO MAT]: Loi chung chi: {sslPolicyErrors}. Da BO QUA vi la moi truong DEV.");
        Console.ResetColor();

        return true;
    }

    // ************ PHƯƠNG THỨC LẮNG NGHE ĐÃ SỬ DỤNG HÀM ĐỌC DÒNG TIN CẬY VÀ FIX LỖI CONSOLE PROMPT ************
    private static async Task ReceiveMessagesAsync(SslStream stream)
    {
        try
        {
            string? response;
            while ((response = await ReadMessageLineAsync(stream)) != null)
            {
                // 1. Dời con trỏ xuống một dòng mới (đảm bảo không ghi đè lên input đang gõ)
                Console.Write("\n");

                // 2. Lưu lại vị trí con trỏ hiện tại (cho trường hợp không dùng Console.Write("\n"))
                // int currentCursorTop = Console.CursorTop;

                // 3. In tin nhắn nhận được (đã được Server định dạng)
                Console.ForegroundColor = ConsoleColor.Yellow;
                //Console.SetCursorPosition(0, currentCursorTop); // Đưa con trỏ về đầu dòng để in
                // Console.Write(new string(' ', Console.WindowWidth)); // Xóa toàn bộ dòng (nếu cần)

                Console.WriteLine(response);
                Console.ResetColor();

                // 4. In lại dấu nhắc nhập liệu (prompt) của người dùng
                Console.Write($"{_currentClientName}: ");
            }

            // Khi ReadMessageLineAsync trả về null (kết nối đóng)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[Thong bao] Server da dong ket noi.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Loi Lang Nghe] {ex.Message}");
            // Bo qua loi khi dong ket noi
        }
    }
}
// hihi
// KHOI CODE ENTRY POINT
public class Program
{
    public static async Task Main(string[] args)
    {
        await SecureChatClient.StartClient();
    }
}