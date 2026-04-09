namespace FlipbookAdmin;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        // ── 컨트롤 선언 ──────────────────────────────
        var lblTitle       = new Label();
        lblStatus          = new Label();
        btnStart           = new Button();
        btnStop            = new Button();
        var lblOrdersTitle = new Label();
        lvOrders           = new ListView();
        var colOrder       = new ColumnHeader();
        var colFrame       = new ColumnHeader();
        var colFilter      = new ColumnHeader();
        var colStatus      = new ColumnHeader();
        var lblLogTitle    = new Label();
        lstLog             = new ListBox();

        this.SuspendLayout();

        // ── 폼 ────────────────────────────────────────
        this.Text            = "50PAGE 어드민";
        this.Size            = new Size(860, 680);
        this.StartPosition   = FormStartPosition.CenterScreen;
        this.BackColor       = Color.FromArgb(245, 245, 240);
        this.Font            = new Font("맑은 고딕", 10f);
        this.MinimumSize     = new Size(700, 500);

        // ── 제목 ──────────────────────────────────────
        lblTitle.Text      = "50PAGE 온라인 주문 어드민";
        lblTitle.Font      = new Font("맑은 고딕", 16f, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(50, 50, 50);
        lblTitle.Location  = new Point(20, 20);
        lblTitle.Size      = new Size(500, 35);

        // ── 상태 표시 ─────────────────────────────────
        lblStatus.Text      = "● 중지됨";
        lblStatus.ForeColor = Color.Gray;
        lblStatus.Font      = new Font("맑은 고딕", 11f, FontStyle.Bold);
        lblStatus.Location  = new Point(20, 62);
        lblStatus.Size      = new Size(200, 28);

        // ── 시작 버튼 ─────────────────────────────────
        btnStart.Text      = "▶ 모니터링 시작";
        btnStart.Location  = new Point(640, 20);
        btnStart.Size      = new Size(180, 42);
        btnStart.BackColor = Color.FromArgb(40, 167, 69);
        btnStart.ForeColor = Color.White;
        btnStart.FlatStyle = FlatStyle.Flat;
        btnStart.Font      = new Font("맑은 고딕", 10f, FontStyle.Bold);
        btnStart.FlatAppearance.BorderSize = 0;
        btnStart.Click    += btnStart_Click;

        // ── 중지 버튼 ─────────────────────────────────
        btnStop.Text      = "■ 중지";
        btnStop.Location  = new Point(640, 68);
        btnStop.Size      = new Size(180, 34);
        btnStop.BackColor = Color.FromArgb(200, 200, 200);
        btnStop.ForeColor = Color.FromArgb(60, 60, 60);
        btnStop.FlatStyle = FlatStyle.Flat;
        btnStop.Font      = new Font("맑은 고딕", 10f);
        btnStop.FlatAppearance.BorderSize = 0;
        btnStop.Enabled   = false;
        btnStop.Click    += btnStop_Click;

        // ── 주문 목록 제목 ────────────────────────────
        lblOrdersTitle.Text      = "주문 목록";
        lblOrdersTitle.Font      = new Font("맑은 고딕", 11f, FontStyle.Bold);
        lblOrdersTitle.ForeColor = Color.FromArgb(50, 50, 50);
        lblOrdersTitle.Location  = new Point(20, 105);
        lblOrdersTitle.Size      = new Size(150, 26);

        // ── 주문 목록 ListView ────────────────────────
        colOrder.Text  = "주문번호";   colOrder.Width  = 200;
        colFrame.Text  = "프레임";     colFrame.Width  = 110;
        colFilter.Text = "필터";       colFilter.Width = 80;
        colStatus.Text = "상태";       colStatus.Width = 200;

        lvOrders.Columns.AddRange(new[] { colOrder, colFrame, colFilter, colStatus });
        lvOrders.View          = View.Details;
        lvOrders.FullRowSelect = true;
        lvOrders.GridLines     = true;
        lvOrders.Location      = new Point(20, 135);
        lvOrders.Size          = new Size(800, 240);
        lvOrders.BackColor     = Color.White;
        lvOrders.BorderStyle   = BorderStyle.FixedSingle;

        // ── 로그 제목 ─────────────────────────────────
        lblLogTitle.Text      = "처리 로그";
        lblLogTitle.Font      = new Font("맑은 고딕", 11f, FontStyle.Bold);
        lblLogTitle.ForeColor = Color.FromArgb(50, 50, 50);
        lblLogTitle.Location  = new Point(20, 390);
        lblLogTitle.Size      = new Size(150, 26);

        // ── 로그 ListBox ──────────────────────────────
        lstLog.Location    = new Point(20, 420);
        lstLog.Size        = new Size(800, 195);
        lstLog.BackColor   = Color.FromArgb(30, 30, 30);
        lstLog.ForeColor   = Color.FromArgb(180, 255, 180);
        lstLog.Font        = new Font("Consolas", 9.5f);
        lstLog.BorderStyle = BorderStyle.None;

        // ── 컨트롤 추가 ───────────────────────────────
        this.Controls.AddRange(new Control[]
        {
            lblTitle, lblStatus, btnStart, btnStop,
            lblOrdersTitle, lvOrders,
            lblLogTitle, lstLog
        });

        this.ResumeLayout(false);
    }

    // ── 필드 ─────────────────────────────────────────
    private Label    lblStatus = null!;
    private Button   btnStart  = null!;
    private Button   btnStop   = null!;
    private ListView lvOrders  = null!;
    private ListBox  lstLog    = null!;
}
