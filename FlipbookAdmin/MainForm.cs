using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace FlipbookAdmin;

public partial class MainForm : Form
{
    // ─────────────────────────────────────────────
    // Firebase 설정 (fifty-page-order 프로젝트)
    // ─────────────────────────────────────────────
    private const string ProjectId = "fifty-page-order-458df";
    private const string ApiKey    = "AIzaSyDk7wDYBG-dXqqOpVNOS0K9FRWmSFA8N-k";
    private const string FirestoreBase =
        $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents";

    // ─────────────────────────────────────────────
    // 경로 설정
    // ─────────────────────────────────────────────
    private readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    private string WorkFolder  => Path.Combine(DesktopPath, "FlipbookOrders");
    private string FfmpegPath  => Path.Combine(DesktopPath, "ffmpeg", "bin", "ffmpeg.exe");

    // ─────────────────────────────────────────────
    // 프레임 / 필터 이름 (웹 order.html 과 동일한 순서)
    // ─────────────────────────────────────────────
    private readonly string[] FrameNames  = { "화이트", "민트", "블랙", "필름", "엽서", "만화", "모래시계", "고양이", "새" };
    private readonly string[] FilterNames = { "원본", "밝게", "흑백" };

    // ─────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────
    private readonly HttpClient _http = new();
    private System.Windows.Forms.Timer? _pollTimer;
    private bool _isProcessing = false;

    // 프린트용
    private List<string> _printFiles   = new();
    private int          _printIndex   = 0;
    private int          _frameIndex   = 0;
    private int          _filterIndex  = 0;

    // ─────────────────────────────────────────────
    // 생성자
    // ─────────────────────────────────────────────
    public MainForm()
    {
        InitializeComponent();
        Directory.CreateDirectory(WorkFolder);
    }

    // ─────────────────────────────────────────────
    // 버튼 이벤트
    // ─────────────────────────────────────────────
    private async void btnStart_Click(object sender, EventArgs e)
    {
        btnStart.Enabled = false;
        btnStop.Enabled  = true;
        SetStatus("● 모니터링 중", Color.Green);

        // 시작 즉시 한 번 확인
        await CheckNewOrders();

        // 이후 30초마다 확인
        _pollTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _pollTimer.Tick += async (_, _) => await CheckNewOrders();
        _pollTimer.Start();
    }

    private void btnStop_Click(object sender, EventArgs e)
    {
        _pollTimer?.Stop();
        btnStart.Enabled = true;
        btnStop.Enabled  = false;
        SetStatus("● 중지됨", Color.Gray);
    }

    // ─────────────────────────────────────────────
    // Firebase Firestore 폴링
    // ─────────────────────────────────────────────
    private async Task CheckNewOrders()
    {
        if (_isProcessing) return;

        try
        {
            Log("새 주문 확인 중...");

            string url  = $"{FirestoreBase}/orders?key={ApiKey}";
            string json = await _http.GetStringAsync(url);

            var orders = ParseOrders(json);
            var newOrder = orders.FirstOrDefault(o => o.Status == "received");

            if (newOrder != null)
                await ProcessOrder(newOrder);
            else
                Log("대기 중인 주문 없음");
        }
        catch (Exception ex)
        {
            Log($"오류: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────
    // 주문 처리 (메인 플로우)
    // ─────────────────────────────────────────────
    private async Task ProcessOrder(Order order)
    {
        _isProcessing = true;

        string orderFolder  = Path.Combine(WorkFolder, order.OrderId);
        string framesFolder = Path.Combine(orderFolder, "frames");

        try
        {
            Directory.CreateDirectory(framesFolder);

            // 주문 목록에 추가
            AddToList(order);

            // 1. 상태: processing
            await UpdateStatus(order.OrderId, "processing");

            // 2. 영상 다운로드
            Log($"[{order.OrderNumber}] 영상 다운로드 중...");
            string videoPath = Path.Combine(orderFolder, "video.mp4");
            await DownloadFile(order.VideoUrl, videoPath);
            Log($"[{order.OrderNumber}] ✅ 다운로드 완료");

            // 3. 프레임 추출
            Log($"[{order.OrderNumber}] 프레임 추출 중...");
            ExtractFrames(videoPath, framesFolder);
            Log($"[{order.OrderNumber}] ✅ 프레임 추출 완료");

            // 4. 24장 균등 선택
            var selected = SelectFrames(framesFolder, 24);
            Log($"[{order.OrderNumber}] ✅ {selected.Count}장 선택 완료");

            // 5. 프린트
            Log($"[{order.OrderNumber}] 🖨️ 프린트 중...");
            await PrintAsync(selected, order.FrameIndex, order.FilterIndex);
            Log($"[{order.OrderNumber}] ✅ 프린트 완료");

            // 6. 상태: printed
            await UpdateStatus(order.OrderId, "printed");
            UpdateListStatus(order.OrderId, "✅ 프린트 완료");
            Log($"[{order.OrderNumber}] 🎉 완료! 제본 후 배송하세요.");
        }
        catch (Exception ex)
        {
            Log($"[{order.OrderNumber}] ❌ 오류: {ex.Message}");
            await UpdateStatus(order.OrderId, "error");
            UpdateListStatus(order.OrderId, "❌ 오류 발생");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    // ─────────────────────────────────────────────
    // 영상 다운로드
    // ─────────────────────────────────────────────
    private async Task DownloadFile(string url, string savePath)
    {
        var bytes = await _http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(savePath, bytes);
    }

    // ─────────────────────────────────────────────
    // FFmpeg 프레임 추출
    // ─────────────────────────────────────────────
    private void ExtractFrames(string videoPath, string outputFolder)
    {
        if (!File.Exists(FfmpegPath))
            throw new FileNotFoundException(
                "FFmpeg를 찾을 수 없습니다.\n바탕화면/ffmpeg/bin/ffmpeg.exe 를 확인하세요.");

        string pattern = Path.Combine(outputFolder, "frame%04d.jpg");
        // fps=12 → 1초에 12장 추출 (업로드 영상 길이에 상관없이 충분한 프레임 확보)
        RunFFmpeg($"-i \"{videoPath}\" -vf fps=12 \"{pattern}\"");
    }

    // ─────────────────────────────────────────────
    // 24장 균등 선택
    // ─────────────────────────────────────────────
    private List<string> SelectFrames(string folder, int count)
    {
        var all = Directory.GetFiles(folder, "*.jpg").OrderBy(f => f).ToList();

        if (all.Count == 0)
            throw new Exception("추출된 프레임이 없습니다. FFmpeg 또는 영상 파일을 확인하세요.");

        if (all.Count <= count)
            return all;

        var result = new List<string>();
        double step = (double)(all.Count - 1) / (count - 1);
        for (int i = 0; i < count; i++)
            result.Add(all[(int)(i * step)]);

        return result;
    }

    // ─────────────────────────────────────────────
    // 프린트
    // ─────────────────────────────────────────────
    private Task PrintAsync(List<string> files, int frameIdx, int filterIdx)
    {
        return Task.Run(() =>
        {
            this.Invoke(() =>
            {
                _printFiles  = new List<string>(files);
                _printFiles.Reverse(); // 뒤에서 앞으로 → 플립북 순서
                _printIndex  = 0;
                _frameIndex  = frameIdx;
                _filterIndex = filterIdx;

                var pd = new PrintDocument();
                pd.PrintPage += OnPrintPage;
                pd.Print();
            });
        });
    }

    private void OnPrintPage(object sender, PrintPageEventArgs e)
    {
        if (_printIndex >= _printFiles.Count)
        {
            e.HasMorePages = false;
            return;
        }

        string path = _printFiles[_printIndex];

        using var original = new Bitmap(path);
        using var filtered = ApplyFilter(original, _filterIndex);

        int w = e.PageBounds.Width;
        int h = e.PageBounds.Height;

        // 사진 그리기
        e.Graphics!.Clear(Color.White);
        e.Graphics.DrawImage(filtered, new Rectangle(0, 0, w, h));

        // 프레임 오버레이 그리기
        string? framePath = GetFramePath(_frameIndex);
        if (framePath != null && File.Exists(framePath))
        {
            using var frame = Image.FromFile(framePath);
            e.Graphics.DrawImage(frame, new Rectangle(0, 0, w, h));
        }

        _printIndex++;
        e.HasMorePages = _printIndex < _printFiles.Count;
    }

    // ─────────────────────────────────────────────
    // 필터 적용
    // ─────────────────────────────────────────────
    private Bitmap ApplyFilter(Bitmap src, int filterIdx)
    {
        return filterIdx switch
        {
            1 => ApplyColorMatrix(src, BrightnessMatrix(1.4f)),  // 밝게
            2 => ApplyColorMatrix(src, GrayscaleMatrix()),        // 흑백
            _ => new Bitmap(src)                                  // 원본
        };
    }

    private Bitmap ApplyColorMatrix(Bitmap src, ColorMatrix cm)
    {
        var ia = new ImageAttributes();
        ia.SetColorMatrix(cm);
        var result = new Bitmap(src.Width, src.Height);
        using var g = Graphics.FromImage(result);
        g.DrawImage(src,
            new Rectangle(0, 0, src.Width, src.Height),
            0, 0, src.Width, src.Height,
            GraphicsUnit.Pixel, ia);
        return result;
    }

    private static ColorMatrix BrightnessMatrix(float b) => new(new[]
    {
        new float[] { b, 0, 0, 0, 0 },
        new float[] { 0, b, 0, 0, 0 },
        new float[] { 0, 0, b, 0, 0 },
        new float[] { 0, 0, 0, 1, 0 },
        new float[] { 0, 0, 0, 0, 1 }
    });

    private static ColorMatrix GrayscaleMatrix() => new(new[]
    {
        new float[] { 0.299f, 0.299f, 0.299f, 0, 0 },
        new float[] { 0.587f, 0.587f, 0.587f, 0, 0 },
        new float[] { 0.114f, 0.114f, 0.114f, 0, 0 },
        new float[] { 0,      0,      0,      1, 0 },
        new float[] { 0,      0,      0,      0, 1 }
    });

    // ─────────────────────────────────────────────
    // 프레임 이미지 경로
    // (기존 키오스크 바탕화면 파일 그대로 사용)
    // ─────────────────────────────────────────────
    private string? GetFramePath(int idx) => idx switch
    {
        0 => Path.Combine(DesktopPath, "whiteFrame.png"),
        1 => Path.Combine(DesktopPath, "mintFrame.png"),
        2 => Path.Combine(DesktopPath, "blackFrame.png"),
        3 => Path.Combine(DesktopPath, "FilmConceptFrameImage.png"),
        4 => Path.Combine(DesktopPath, "PostcardConceptFrameImage.png"),
        5 => Path.Combine(DesktopPath, "CartoonConceptFrameImage.png"),
        6 => Path.Combine(DesktopPath, "mintFrame.png"),
        7 => Path.Combine(DesktopPath, "whiteFrame.png"),
        8 => Path.Combine(DesktopPath, "pinkFrame.png"),
        _ => null
    };

    // ─────────────────────────────────────────────
    // Firestore 상태 업데이트
    // ─────────────────────────────────────────────
    private async Task UpdateStatus(string orderId, string status)
    {
        string url  = $"{FirestoreBase}/orders/{orderId}?updateMask.fieldPaths=status&key={ApiKey}";
        string body = $"{{\"fields\":{{\"status\":{{\"stringValue\":\"{status}\"}}}}}}";
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        await _http.PatchAsync(url, content);
    }

    // ─────────────────────────────────────────────
    // Firestore 주문 파싱
    // ─────────────────────────────────────────────
    private List<Order> ParseOrders(string json)
    {
        var result = new List<Order>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("documents", out var docs)) return result;

            foreach (var d in docs.EnumerateArray())
            {
                if (!d.TryGetProperty("fields", out var f)) continue;
                result.Add(new Order
                {
                    OrderId     = Str(f, "orderId"),
                    OrderNumber = Str(f, "orderNumber"),
                    Status      = Str(f, "status"),
                    VideoUrl    = Str(f, "videoUrl"),
                    FrameIndex  = Int(f, "frameIndex"),
                    FilterIndex = Int(f, "filterIndex"),
                });
            }
        }
        catch { /* 파싱 오류는 무시 */ }
        return result;
    }

    private static string Str(JsonElement f, string key)
    {
        if (f.TryGetProperty(key, out var v) && v.TryGetProperty("stringValue", out var s))
            return s.GetString() ?? "";
        return "";
    }

    private static int Int(JsonElement f, string key)
    {
        if (f.TryGetProperty(key, out var v) && v.TryGetProperty("integerValue", out var n))
            return int.TryParse(n.GetString(), out int r) ? r : 0;
        return 0;
    }

    // ─────────────────────────────────────────────
    // FFmpeg 실행
    // ─────────────────────────────────────────────
    private void RunFFmpeg(string args)
    {
        var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = FfmpegPath,
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            }
        };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
    }

    // ─────────────────────────────────────────────
    // UI 헬퍼
    // ─────────────────────────────────────────────
    private void SetStatus(string text, Color color)
    {
        lblStatus.Text      = text;
        lblStatus.ForeColor = color;
    }

    private void Log(string msg)
    {
        if (lstLog.InvokeRequired) { lstLog.Invoke(() => Log(msg)); return; }
        lstLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
        if (lstLog.Items.Count > 200) lstLog.Items.RemoveAt(lstLog.Items.Count - 1);
    }

    private void AddToList(Order o)
    {
        if (lvOrders.InvokeRequired) { lvOrders.Invoke(() => AddToList(o)); return; }
        string frame  = o.FrameIndex  < FrameNames.Length  ? FrameNames[o.FrameIndex]   : "?";
        string filter = o.FilterIndex < FilterNames.Length ? FilterNames[o.FilterIndex] : "?";
        var item = new ListViewItem(o.OrderNumber);
        item.SubItems.Add(frame);
        item.SubItems.Add(filter);
        item.SubItems.Add("⏳ 처리 중");
        item.Name = o.OrderId;
        lvOrders.Items.Insert(0, item);
    }

    private void UpdateListStatus(string orderId, string status)
    {
        if (lvOrders.InvokeRequired) { lvOrders.Invoke(() => UpdateListStatus(orderId, status)); return; }
        var item = lvOrders.Items[orderId];
        if (item != null && item.SubItems.Count >= 4)
            item.SubItems[3].Text = status;
    }
}

// ─────────────────────────────────────────────
// 주문 모델
// ─────────────────────────────────────────────
public class Order
{
    public string OrderId     { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    public string Status      { get; set; } = "";
    public string VideoUrl    { get; set; } = "";
    public int    FrameIndex  { get; set; }
    public int    FilterIndex { get; set; }
}
