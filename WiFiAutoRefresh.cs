// ============================================================================
// WiFi 自动刷新器  (单文件 C# WinForms 程序)
// ----------------------------------------------------------------------------
// 功能：
//   1. 每 5 秒调用 wlanapi 触发一次 WiFi 扫描，并实时刷新可见网络列表
//   2. 列表分页（每页 10 个），可上一页 / 下一页
//   3. 已连接的网络用绿色高亮
//   4. "自动滚动" 按设定秒数自动翻页轮换
//   5. 三种“自动运行”方式（互斥，一键启用/一键删除）：
//      ① 注册表 Run 键——当前用户登录后自动启动（窗口界面）
//      ② 系统级计划任务——开机即以 SYSTEM 身份后台运行本 EXE（-bg），不依赖登录
//      ③ Windows 服务——以服务方式常驻后台（-svc），开机自动启动
//   6. 显示各热点真实信号强度(%)，已连接置顶、其余按信号从强到弱排序
//
// 编译（在 VS Code 集成终端里执行）：
//   cd /d C:\Scripts\WiFiRefresh
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:WiFiAutoRefresh.exe WiFiAutoRefresh.cs /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.ServiceProcess.dll /win32icon:WiFiAutoRefresh.ico
//
// 运行参数：无参 = 窗口界面；-bg = 无窗口后台扫描（供计划任务）；-svc = Windows 服务宿主
//
// 注意：
//   - 这是 C# 5 (.NET Framework 4.x) 语法，不能用 => 表达式体成员 / 局部函数
//   - netsh wlan 输出是混合编码：本地化标签(信号/身份验证等)走系统 ANSI(GBK)，
//     SSID 名称是路由器广播的原始字节(中文一般 UTF-8)。不能单一编码读全文，
//     必须 Latin1 无损取字节后逐行双解码(标签用 GBK、SSID 值用 UTF-8)
// ============================================================================

using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Microsoft.Win32;
using System.ServiceProcess;

// wlanapi.dll 的 P/Invoke 声明
static class WlanApi {
    [DllImport("wlanapi.dll")] public static extern uint WlanOpenHandle(uint v, IntPtr p, out uint neg, out IntPtr h);
    [DllImport("wlanapi.dll")] public static extern uint WlanEnumInterfaces(IntPtr h, IntPtr p, out IntPtr pp);
    [DllImport("wlanapi.dll")] public static extern void WlanFreeMemory(IntPtr p);
    [DllImport("wlanapi.dll")] public static extern uint WlanScan(IntPtr h, Guid g, IntPtr s, IntPtr i, IntPtr r);
    [DllImport("wlanapi.dll")] public static extern uint WlanCloseHandle(IntPtr h, IntPtr p);
}

// 一个 WiFi 网络的信息
class Network {
    public string SSID;
    public string Auth;
    public int Signal;
    public bool Connected;
    public int MissCount; // 连续未出现在扫描结果中的轮数（平滑合并用）
}

class App : Form {
    TextBox logBox;
    ListView netList;
    ComboBox cmbAutoMode;
    Button btnAutoOn, btnAutoOff;
    Label lblAutoState;
    CheckBox chkEnabled;
    CheckBox chkAutoCycle;
    NumericUpDown numCycleSec;
    Button btnPrev, btnNext;
    Label statusLabel, pageLabel;
    Thread scanThread;
    System.Windows.Forms.Timer cycleTimer;
    readonly List<Network> allNetworks = new List<Network>();
    int currentPage = 0;
    const int PageSize = 10;
    volatile bool running = false;
    // 自动运行三模式名称常量
    const string RunRegValueName = "WiFiAutoRefresh";
    const string SvcTaskName = "WiFiAutoRefreshSvc";
    const string WindowsSvcName = "WiFiAutoRefreshService";

    public App() {
        Text = "WiFi 自动刷新器";
        Size = new Size(640, 560);
        StartPosition = FormStartPosition.CenterScreen;
        // 窗口图标：优先用 EXE 自带的 WiFi 图标（编译时 /win32icon 嵌入），提取不到才回退默认
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { Icon = SystemIcons.Application; }
        BackColor = Color.White;

        // 使用 TableLayoutPanel 彻底绕开 Dock=Top 的堆叠顺序坑点
        // 五行从上到下：状态栏 / 自动运行设置 / 分页栏 / 网络列表 (Fill) / 日志框
        var layout = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.White,
            Padding = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            AutoSize = false
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        // Row 0: 状态栏
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        // Row 1: 自动运行设置
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        // Row 2: 分页栏
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        // Row 3: 网络列表 (填充剩余空间)
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        // Row 4: 日志框
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140F));

        // 顶部栏：状态 / 开启扫描
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = false, Padding = new Padding(8, 6, 8, 0) };
        statusLabel = new Label { Text = "状态: 已停止", AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 9F) };
        chkEnabled = new CheckBox { Text = "开启扫描", AutoSize = true, Margin = new Padding(10, 6, 0, 0) };
        chkEnabled.CheckedChanged += ChkEnabled_CheckedChanged;
        top.Controls.AddRange(new Control[] { statusLabel, chkEnabled });

        // 自动运行设置栏：选择方式(注册表Run/计划任务/服务) + 一键启用 / 一键删除
        var autoBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = false, Padding = new Padding(8, 4, 8, 0) };
        var lblAuto = new Label { Text = "自动运行方式:", AutoSize = true, Margin = new Padding(0, 6, 0, 0), ForeColor = Color.DimGray, Font = new Font("Segoe UI", 9F) };
        cmbAutoMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190, Margin = new Padding(6, 3, 0, 0) };
        cmbAutoMode.Items.Add("注册表 Run（登录启动）");
        cmbAutoMode.Items.Add("计划任务（系统级）");
        cmbAutoMode.Items.Add("Windows 服务（常驻）");
        cmbAutoMode.SelectedIndex = 0;
        btnAutoOn = new Button { Text = "一键启用", AutoSize = true, Margin = new Padding(8, 2, 0, 0) };
        btnAutoOn.Click += BtnAutoOn_Click;
        btnAutoOff = new Button { Text = "一键删除", AutoSize = true, Margin = new Padding(6, 2, 0, 0) };
        btnAutoOff.Click += BtnAutoOff_Click;
        lblAutoState = new Label { Text = "", AutoSize = true, Margin = new Padding(8, 6, 0, 0), ForeColor = Color.DarkGreen, Font = new Font("Segoe UI", 9F) };
        autoBar.Controls.AddRange(new Control[] { lblAuto, cmbAutoMode, btnAutoOn, btnAutoOff, lblAutoState });

        // 分页栏：上一页 / 下一页 / 第 X/Y 页 / 自动滚动 / 间隔
        var pageBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = false, Padding = new Padding(8, 2, 8, 0) };
        btnPrev = new Button { Text = "< 上一页", AutoSize = true };        btnPrev.Click += BtnPrev_Click;
        btnNext = new Button { Text = "下一页 >", AutoSize = true, Margin = new Padding(6, 4, 0, 0) };
        btnNext.Click += BtnNext_Click;
        pageLabel = new Label { Text = "第 1/1 页 (共 0 个)", AutoSize = true, Margin = new Padding(10, 6, 0, 0), ForeColor = Color.DarkBlue, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        chkAutoCycle = new CheckBox { Text = "自动滚动", AutoSize = true, Margin = new Padding(20, 4, 0, 0) };
        chkAutoCycle.CheckedChanged += ChkAutoCycle_CheckedChanged;
        var lblSec = new Label { Text = "间隔(秒):", AutoSize = true, Margin = new Padding(10, 6, 0, 0) };
        numCycleSec = new NumericUpDown { Minimum = 1, Maximum = 60, Value = 3, Width = 50, Margin = new Padding(4, 2, 0, 0) };
        numCycleSec.ValueChanged += NumCycleSec_ValueChanged;
        pageBar.Controls.AddRange(new Control[] { btnPrev, btnNext, pageLabel, chkAutoCycle, lblSec, numCycleSec });

        // 网络列表
        netList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, Font = new Font("Segoe UI", 9F), GridLines = true, Margin = new Padding(0) };
        netList.Columns.Add("SSID 名称", 210);
        netList.Columns.Add("信号", 70);
        netList.Columns.Add("加密", 110);
        netList.Columns.Add("状态", 70);

        // 日志框
        logBox = new TextBox {
            Dock = DockStyle.Fill, ReadOnly = true, Multiline = true,
            ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 8F),
            BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.LightGreen, Margin = new Padding(0)
        };

        // 按表格行顺序填入控件
        layout.Controls.Add(top, 0, 0);
        layout.Controls.Add(autoBar, 0, 1);
        layout.Controls.Add(pageBar, 0, 2);
        layout.Controls.Add(netList, 0, 3);
        layout.Controls.Add(logBox, 0, 4);
        Controls.Add(layout);

        cycleTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        cycleTimer.Tick += CycleTimer_Tick;

        // 订阅完事件后再设 Checked，触发 Start() 默认开启扫描
        chkEnabled.Checked = true;
        RefreshAutoState();
    }

    void ChkEnabled_CheckedChanged(object sender, EventArgs e) {
        if (chkEnabled.Checked) Start(); else Stop();
    }

    void BtnPrev_Click(object sender, EventArgs e) { ChangePage(-1); }
    void BtnNext_Click(object sender, EventArgs e) { ChangePage(1); }

    void ChkAutoCycle_CheckedChanged(object sender, EventArgs e) {
        cycleTimer.Enabled = chkAutoCycle.Checked;
        if (chkAutoCycle.Checked) Log("自动滚动已启用 (每 " + (int)numCycleSec.Value + " 秒)");
        else Log("自动滚动已停用");
    }

    void NumCycleSec_ValueChanged(object sender, EventArgs e) {
        cycleTimer.Interval = (int)numCycleSec.Value * 1000;
    }

    void CycleTimer_Tick(object sender, EventArgs e) { ChangePage(1); }

    void ChangePage(int delta) {
        int totalPages = Math.Max(1, (int)Math.Ceiling((double)allNetworks.Count / PageSize));
        currentPage = (currentPage + delta + totalPages) % totalPages;
        RefreshList();
    }

    void RefreshList() {
        if (InvokeRequired) { Invoke(new Action(RefreshList)); return; }
        netList.BeginUpdate();
        netList.Items.Clear();
        int totalPages = Math.Max(1, (int)Math.Ceiling((double)allNetworks.Count / PageSize));
        pageLabel.Text = "第 " + (currentPage + 1) + "/" + totalPages + " 页 (共 " + allNetworks.Count + " 个)";
        btnPrev.Enabled = totalPages > 1;
        btnNext.Enabled = totalPages > 1;
        int start = currentPage * PageSize;
        int end = Math.Min(start + PageSize, allNetworks.Count);
        for (int i = start; i < end; i++) {
            var n = allNetworks[i];
            var item = new ListViewItem(n.SSID);
            ListViewItem.ListViewSubItem sigItem = item.SubItems.Add(n.Signal + "%");
            item.SubItems.Add(n.Auth);
            item.SubItems.Add(n.Connected ? "已连接" : "");
            if (n.Connected) {
                // 已连接：整行浅绿高亮
                item.UseItemStyleForSubItems = true;
                item.BackColor = Color.FromArgb(200, 255, 200);
            } else {
                // 信号按档位着色：>=70 绿 / >=40 橙 / <40 红
                item.UseItemStyleForSubItems = false;
                sigItem.ForeColor = n.Signal >= 70 ? Color.SeaGreen
                    : (n.Signal >= 40 ? Color.DarkOrange : Color.Firebrick);
            }
            netList.Items.Add(item);
        }
        netList.EndUpdate();
    }

    void Start() {
        if (running) return;
        running = true;
        statusLabel.Text = "状态: 扫描中";
        statusLabel.ForeColor = Color.Green;
        scanThread = new Thread(ScanLoop) { IsBackground = true };
        scanThread.Start();
        Log("启动扫描 (每 5 秒一次)...");
    }

    void Stop() {
        running = false;
        statusLabel.Text = "状态: 已停止";
        statusLabel.ForeColor = Color.Gray;
        Log("已停止。");
    }

    // 后台扫描线程：触发 wlanapi 扫描 + 解析 netsh 列表
    void ScanLoop() {
        IntPtr h = IntPtr.Zero;
        while (running) {
            try {
                uint v = 0;
                var rc = WlanApi.WlanOpenHandle(2, IntPtr.Zero, out v, out h);
                if (rc != 0) { SetLog("OPENFAIL " + rc); Thread.Sleep(5000); continue; }
                IntPtr pp = IntPtr.Zero;
                rc = WlanApi.WlanEnumInterfaces(h, IntPtr.Zero, out pp);
                if (rc != 0) { WlanApi.WlanCloseHandle(h, IntPtr.Zero); SetLog("ENUMFAIL " + rc); Thread.Sleep(5000); continue; }
                uint num = BitConverter.ToUInt32(ReadBytes(pp, 0, 4), 0);
                int ok = 0;
                for (uint i = 0; i < num; i++) {
                    Guid g = new Guid(ReadBytes(pp, 8 + (int)i * 532, 16));
                    rc = WlanApi.WlanScan(h, g, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    if (rc == 0) ok++;
                }
                WlanApi.WlanFreeMemory(pp);
                WlanApi.WlanCloseHandle(h, IntPtr.Zero);
                h = IntPtr.Zero;
                SetLog("[" + DateTime.Now.ToString("HH:mm:ss") + "] SCAN ok=" + ok + "/" + num);
                // WlanScan 是异步的，驱动扫完全部信道需要 2~4 秒；等 2 秒再读，
                // 避免 netsh 拿到扫描中途的半截缓存导致列表忽多忽少
                Thread.Sleep(2000);
                List<Network> nets = GetNetworks();
                SetNetworks(nets);
            } catch (Exception ex) {
                SetLog("ERR: " + ex.Message);
            }
            Thread.Sleep(5000);
        }
    }

    byte[] ReadBytes(IntPtr p, int offset, int count) {
        var b = new byte[count];
        Marshal.Copy(IntPtr.Add(p, offset), b, 0, count);
        return b;
    }

    // 用 netsh 拿到当前可见网络列表
    List<Network> GetNetworks() {
        var nets = new List<Network>();
        try {
            // mode=bssid 才会输出每个热点的信号强度(Signal)，基本命令不带信号，必须加
            var psi = new ProcessStartInfo("netsh", "wlan show networks mode=bssid");
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            // 混合编码：标签 GBK、SSID 名 UTF-8。用 Latin1(28591) 无损取原始字节，
            // 再交给 ParseNetshNetworks 逐行双解码，避免单编码读全文必然错一半
            psi.StandardOutputEncoding = Encoding.GetEncoding(28591);
            using (var p = Process.Start(psi)) {
                string rawText = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                byte[] raw = Encoding.GetEncoding(28591).GetBytes(rawText);
                nets = ParseNetshNetworks(raw);
            }
            string connectedSSID = GetConnectedSSID();
            if (!string.IsNullOrEmpty(connectedSSID)) {
                foreach (var n in nets) {
                    if (n.SSID == connectedSSID) n.Connected = true;
                }
            }
            // 排序：已连接的网络置顶，其余按信号强度从强到弱
            nets.Sort(CompareBySignal);
        } catch (Exception ex) {
            SetLog("GETNET ERR: " + ex.Message);
        }
        return nets;
    }

    // 用 netsh 拿到当前已连接的 SSID（混合编码：标签/状态值 GBK、SSID 名 UTF-8，逐行双解码）
    string GetConnectedSSID() {
        try {
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces");
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            // Latin1 无损取原始字节，再分别按 GBK/UTF-8 解码逐行对齐处理
            psi.StandardOutputEncoding = Encoding.GetEncoding(28591);
            using (var p = Process.Start(psi)) {
                string rawText = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                byte[] raw = Encoding.GetEncoding(28591).GetBytes(rawText);
                var gbk = Encoding.GetEncoding(936);
                string gbkAll = gbk.GetString(raw);
                string utf8All = Encoding.UTF8.GetString(raw);
                string[] gLines = gbkAll.Split('\n');
                string[] uLines = utf8All.Split('\n');
                int n = Math.Min(gLines.Length, uLines.Length);
                bool connected = false;
                for (int i = 0; i < n; i++) {
                    string gLine = gLines[i].Trim();
                    string uLine = uLines[i].Trim();
                    // 状态行：英文 State / 中文 状态（GBK 标签）；值 connected/已连接
                    if (gLine.StartsWith("State") || gLine.StartsWith("状态")) {
                        connected = gLine.IndexOf("connected", StringComparison.OrdinalIgnoreCase) >= 0
                            || gLine.Contains("已连接") || gLine.Contains("已連線");
                    } else if (connected && (uLine.StartsWith("SSID"))) {
                        // SSID 行：标签是 ASCII，值可能是 UTF-8 中文；从 UTF-8 视图取值
                        int colonIdx = uLine.IndexOf(':');
                        if (colonIdx > 0) {
                            string s = uLine.Substring(colonIdx + 1).Trim();
                            if (!string.IsNullOrEmpty(s) && !s.StartsWith("BSSID")) return s;
                        }
                    }
                }
            }
        } catch { }
        return null;
    }

    // 排序比较：已连接优先，其次信号从强到弱
    static int CompareBySignal(Network a, Network b) {
        if (a.Connected != b.Connected) return a.Connected ? -1 : 1;
        return b.Signal.CompareTo(a.Signal);
    }

    // 解析 netsh 输出（混合编码）：
    //   - 标签行(身份验证/信号等)是系统 ANSI=GBK(中文系统)或纯 ASCII(英文系统) → 用 GBK 解码后匹配
    //   - SSID 名称是路由器原始字节，中文热点一般 UTF-8 → 该行 UTF-8 解码正常则取 UTF-8，否则回退 GBK
    // 两种解码按 '\n' 拆分后行数一致(GBK/UTF-8 多字节都不含 0x0A)，可按行号对齐逐行处理
    List<Network> ParseNetshNetworks(byte[] raw) {
        var nets = new List<Network>();
        var gbk = Encoding.GetEncoding(936);
        string gbkAll = gbk.GetString(raw);
        string utf8All = Encoding.UTF8.GetString(raw); // 宽松解码：GBK 标签字节变 U+FFFD，SSID(UTF-8)正常
        string[] gLines = gbkAll.Split('\n');
        string[] uLines = utf8All.Split('\n');
        int n = Math.Min(gLines.Length, uLines.Length);
        Network current = null;
        for (int i = 0; i < n; i++) {
            string gLine = gLines[i].Trim();
            string uLine = uLines[i].Trim();
            // 新 SSID 块：标签 "SSID n : " 是 ASCII，两种解码都正常
            if (gLine.StartsWith("SSID") && !gLine.StartsWith("BSSID") && gLine.Contains(":")) {
                int gi = gLine.IndexOf(':');
                int ui = uLine.IndexOf(':');
                if (gi < 0) continue;
                string gVal = gLine.Substring(gi + 1).Trim();
                string uVal = ui >= 0 ? uLine.Substring(ui + 1).Trim() : gVal;
                // 该行 UTF-8 解码含替换符说明 SSID 本身不是 UTF-8(老设备 GBK 名)，回退 GBK 取值
                string ssid = (uLine.IndexOf('\uFFFD') >= 0) ? gVal : uVal;
                if (!string.IsNullOrEmpty(ssid)) {
                    current = new Network { SSID = ssid, Auth = "开放", Signal = 0, Connected = false };
                    nets.Add(current);
                } else {
                    current = null; // 隐藏 SSID 空名，跳过其块
                }
                continue;
            }
            if (current == null) continue;

            // 加密方式（英文 Authentication / 中文 身份验证/验证）：标签 GBK，值"WPA2 - 个人"等也 GBK
            if (gLine.StartsWith("Authentication") || gLine.StartsWith("身份验证") || gLine.StartsWith("验证")) {
                int colonIdx = gLine.IndexOf(':');
                if (colonIdx > 0) {
                    string auth = gLine.Substring(colonIdx + 1).Trim();
                    if (string.IsNullOrEmpty(auth)
                        || auth.Equals("Open", StringComparison.OrdinalIgnoreCase)
                        || auth.Contains("开放") || auth.Contains("无")) current.Auth = "开放";
                    else current.Auth = auth;
                }
                continue;
            }
            // 信号强度（英文 Signal / 中文 信号）：值 "NN%" 纯 ASCII，取数字即可
            if (gLine.StartsWith("Signal") || gLine.StartsWith("信号")) {
                int colonIdx = gLine.IndexOf(':');
                if (colonIdx > 0) {
                    string signalStr = gLine.Substring(colonIdx + 1).Trim().Replace("%", "").Trim();
                    int s;
                    // 同一 SSID 可能有多个 BSSID，各带信号值；取最强的一个作为该网络的代表信号
                    if (int.TryParse(signalStr, out s) && s > current.Signal) current.Signal = s;
                }
                continue;
            }
        }
        return nets; // 排序统一在 GetNetworks() 标记完已连接之后做
    }

    // 平滑合并扫描结果：本轮仍在的更新信息；短暂消失(<=2轮)的保留不闪没；
    // 连续 3 轮未出现才移除（已连接网络永不删）。解决扫描波动导致的名称忽隐忽现
    void SetNetworks(List<Network> nets) {
        if (InvokeRequired) { Invoke(new Action<List<Network>>(SetNetworks), nets); return; }
        var merged = new List<Network>();
        // 1) 本轮扫到的：更新旧条目或新增
        foreach (var n in nets) {
            Network old = FindBySSID(n.SSID);
            if (old != null) {
                old.Signal = n.Signal;
                old.Auth = n.Auth;
                old.Connected = n.Connected;
                old.MissCount = 0;
                merged.Add(old);
            } else {
                n.MissCount = 0;
                merged.Add(n);
            }
        }
        // 2) 旧列表本轮没出现的：清掉已连接标记(本轮没扫到=不再是当前连接，
        //    真连着的话下轮 nets 会带回 true)，给 2 轮缓冲，超过才移除
        foreach (var old in allNetworks) {
            if (merged.Contains(old)) continue;
            old.Connected = false;
            old.MissCount++;
            if (old.MissCount <= 2) merged.Add(old);
        }
        merged.Sort(CompareBySignal);
        allNetworks.Clear();
        allNetworks.AddRange(merged);
        int totalPages = Math.Max(1, (int)Math.Ceiling((double)allNetworks.Count / PageSize));
        if (currentPage >= totalPages) currentPage = 0;
        RefreshList();
    }

    // 按 SSID 精确匹配旧列表条目（UI 线程内调用，列表很小直接线性查找）
    Network FindBySSID(string ssid) {
        foreach (var x in allNetworks) if (x.SSID == ssid) return x;
        return null;
    }

    void SetLog(string msg) {
        if (InvokeRequired) { Invoke(new Action<string>(SetLog), msg); return; }
        logBox.AppendText(msg + Environment.NewLine);
        logBox.SelectionStart = logBox.Text.Length;
        logBox.ScrollToCaret();
    }

    void Log(string msg) { SetLog(msg); }

    // ================= 自动运行三模式（互斥） =================
    // 模式0: 注册表 Run 键 —— 当前用户登录后自动启动（窗口界面）
    // 模式1: 系统级计划任务 —— 开机即以 SYSTEM 运行本 EXE（-bg 无窗口后台），不依赖登录
    // 模式2: Windows 服务 —— 以服务方式常驻后台（-svc），开机自动启动

    static string ExePath() { return Process.GetCurrentProcess().MainModule.FileName; }

    // 执行外部命令并等待结束，返回退出码（失败/异常返回 -1）
    int RunHidden(string file, string args) {
        try {
            var psi = new ProcessStartInfo(file, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (var p = Process.Start(psi)) {
                string errTxt = p.StandardError.ReadToEnd();
                p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0 && errTxt.Length > 0) Log("  " + errTxt.Trim());
                return p.ExitCode;
            }
        } catch (Exception ex) { Log("  调用失败: " + ex.Message); return -1; }
    }

    bool RegRunActive() {
        try {
            using (var rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                return rk != null && rk.GetValue(RunRegValueName) != null;
        } catch { return false; }
    }

    bool SvcTaskActive() { return RunHidden("schtasks.exe", "/query /tn " + SvcTaskName + " /fo list") == 0; }
    bool WinSvcActive()  { return RunHidden("sc.exe", "query " + WindowsSvcName) == 0; }

    int DetectActiveMode() {
        if (RegRunActive()) return 0;
        if (SvcTaskActive()) return 1;
        if (WinSvcActive()) return 2;
        return -1;
    }

    // 启动/状态变化后刷新右侧状态文字（若检测到已启用则下拉框切到对应项）
    void RefreshAutoState() {
        if (InvokeRequired) { Invoke(new Action(RefreshAutoState)); return; }
        int m = DetectActiveMode();
        string[] names = { "注册表 Run", "系统级计划任务", "Windows 服务" };
        if (m >= 0) {
            cmbAutoMode.SelectedIndex = m;
            lblAutoState.Text = "已启用: " + names[m];
            lblAutoState.ForeColor = Color.DarkGreen;
        } else {
            lblAutoState.Text = "未启用（仅本次运行）";
            lblAutoState.ForeColor = Color.Gray;
        }
    }

    void RemoveAutoMode(int mode) {
        if (mode == 0) {
            try {
                using (var rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    if (rk != null) rk.DeleteValue(RunRegValueName, false);
                Log("已删除注册表 Run 键。");
            } catch (Exception ex) { Log("删除注册表键失败: " + ex.Message); }
        } else if (mode == 1) {
            RunHidden("schtasks.exe", "/delete /tn " + SvcTaskName + " /f");
            Log("已删除计划任务 " + SvcTaskName + "。");
        } else if (mode == 2) {
            RunHidden("sc.exe", "stop " + WindowsSvcName);
            RunHidden("sc.exe", "delete " + WindowsSvcName);
            Log("已删除 Windows 服务 " + WindowsSvcName + "。");
        }
    }

    void BtnAutoOn_Click(object sender, EventArgs e) {
        int mode = cmbAutoMode.SelectedIndex;
        string exe = "\"" + ExePath() + "\"";
        if (mode == 0) {
            try {
                using (var rk = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    rk.SetValue(RunRegValueName, exe);
                Log("一键启用成功: 注册表 Run（当前用户登录后自动启动）");
            } catch (Exception ex2) { Log("写入注册表失败: " + ex2.Message); }
        } else if (mode == 1) {
            // 计划任务以 SYSTEM 身份启动 -bg 后台实例（无窗口，不依赖登录）
            // /tr 值整体用引号包裹，内部 exe 路径引号需 \" 转义（与命令行解析一致）
            string tr = "/create /tn " + SvcTaskName + " /tr \"\\\"" + ExePath() + "\\\" -bg\" /sc ONSTART /ru SYSTEM /rl HIGHEST /f";
            int rc = RunHidden("schtasks.exe", tr);
            Log(rc == 0 ? "一键启用成功: 系统级计划任务（开机后台运行，不依赖登录）" : "启用失败: 请以管理员身份运行本程序后重试");
        } else if (mode == 2) {
            string scArgs = "create " + WindowsSvcName + " binPath= \"\\\"" + ExePath() + "\\\" -svc\" start= auto";
            int rc = RunHidden("sc.exe", scArgs);
            if (rc == 0) {
                RunHidden("sc.exe", "start " + WindowsSvcName);
                Log("一键启用成功: Windows 服务（开机常驻后台）");
            } else {
                Log("启用失败: 请以管理员身份运行本程序后重试");
            }
        }
        RefreshAutoState();
    }

    void BtnAutoOff_Click(object sender, EventArgs e) {
        RemoveAutoMode(cmbAutoMode.SelectedIndex);
        RefreshAutoState();
    }

    // ---------------- 无窗口后台循环（-bg 计划任务 / Windows 服务共用） ----------------
    // 不建窗体不解析列表，只周期性触发 wlanapi 扫描，日志写入 exe 同目录 wifi_bg.log
    internal static volatile bool bgStop = false;

    internal static byte[] BgReadBytes(IntPtr p, int off, int cnt) {
        var b = new byte[cnt];
        Marshal.Copy(new IntPtr(p.ToInt64() + off), b, 0, cnt);
        return b;
    }

    internal static void RunBackgroundLoop() {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wifi_bg.log");
        try { File.AppendAllText(logPath, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] BG start\r\n"); } catch { }
        while (!bgStop) {
            try {
                uint v = 0; IntPtr h = IntPtr.Zero;
                uint rc = WlanApi.WlanOpenHandle(2, IntPtr.Zero, out v, out h);
                if (rc == 0) {
                    IntPtr pp = IntPtr.Zero;
                    rc = WlanApi.WlanEnumInterfaces(h, IntPtr.Zero, out pp);
                    if (rc == 0) {
                        uint num = BitConverter.ToUInt32(BgReadBytes(pp, 0, 4), 0);
                        int ok = 0;
                        for (uint i = 0; i < num; i++) {
                            Guid g = new Guid(BgReadBytes(pp, 8 + (int)i * 532, 16));
                            if (WlanApi.WlanScan(h, g, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) == 0) ok++;
                        }
                        WlanApi.WlanFreeMemory(pp);
                        try { File.AppendAllText(logPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] SCAN ok=" + ok + "/" + num + "\r\n"); } catch { }
                    }
                    WlanApi.WlanCloseHandle(h, IntPtr.Zero);
                }
            } catch (Exception ex) {
                try { File.AppendAllText(logPath, "ERR " + ex.Message + "\r\n"); } catch { }
            }
            Thread.Sleep(5000);
        }
        try { File.AppendAllText(logPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] BG stop\r\n"); } catch { }
    }

    [STAThread]
    static void Main(string[] args) {
        bool asSvc = false, asBg = false;
        if (args != null) {
            foreach (string a in args) {
                if (a == "-svc") asSvc = true;
                else if (a == "-bg") asBg = true;
            }
        }
        if (asSvc) { ServiceBase.Run(new WifiSvc()); return; }
        if (asBg) { RunBackgroundLoop(); return; }
        Application.EnableVisualStyles();
        Application.Run(new App());
    }
}

// Windows 服务宿主（-svc 参数）：安装命令见上方注释（sc create WiFiAutoRefreshService ...）
class WifiSvc : ServiceBase {
    public WifiSvc() { ServiceName = "WiFiAutoRefreshService"; }
    protected override void OnStart(string[] args) {
        App.bgStop = false;
        var t = new Thread(App.RunBackgroundLoop) { IsBackground = true };
        t.Start();
    }
    protected override void OnStop() { App.bgStop = true; }
}
