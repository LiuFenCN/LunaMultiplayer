using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace LmpServerManager
{
    public partial class MainForm : Form
    {
        private string serverDir;
        private Process? serverProcess;
        private DateTime? startTime;
        private System.Windows.Forms.Timer statusTimer = new();

        // 服务器页
        private TextBox txtServerDir = new();
        private Button btnBrowse = new();
        private Button btnStart = new();
        private Button btnStop = new();
        private Label lblStatus = new();
        private Label lblPid = new();
        private Label lblUptime = new();
        private Label lblPort = new();

        // 配置页
        private ComboBox cboConfig = new();
        private TextBox txtConfig = new();
        private Button btnSaveConfig = new();

        // 白名单页
        private CheckBox chkAllowNonListed = new();
        private ListBox lstDll = new();
        private TextBox txtAddDll = new();
        private Button btnAddDll = new();
        private Button btnRemoveDll = new();
        private Button btnSaveMod = new();

        // 日志页
        private TextBox txtLog = new();
        private Button btnRefreshLog = new();

        // 清档页
        private Button btnReset = new();
        private Label lblResetHint = new();

        public MainForm()
        {
            serverDir = DetectServerDir();
            InitializeComponent();
            LoadConfigFiles();
            LoadModControl();
            statusTimer.Interval = 1000;
            statusTimer.Tick += (s, e) => UpdateStatus();
            statusTimer.Start();
            UpdateStatus();
        }

        // ---------- 目录探测 ----------
        private string DetectServerDir()
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(exe))
                {
                    string dir = Path.GetDirectoryName(exe) ?? "";
                    if (File.Exists(Path.Combine(dir, "Server.exe"))) return dir;
                }
            }
            catch { }
            string baseDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
            if (File.Exists(Path.Combine(baseDir, "Server.exe"))) return baseDir;
            foreach (var drv in DriveInfo.GetDrives())
            {
                string p = Path.Combine(drv.RootDirectory.FullName, "Desktop", "LMPServer");
                if (File.Exists(Path.Combine(p, "Server.exe"))) return p;
            }
            return baseDir;
        }

        // ---------- UI 布局 ----------
        private void InitializeComponent()
        {
            Text = "LMP 服务端管理器";
            Width = 820; Height = 600;
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.AddRange(new[] {
                MakeServerTab(), MakeConfigTab(), MakeModTab(), MakeLogTab(), MakeResetTab()
            });
            Controls.Add(tabs);

            // 顶部路径条
            var top = new Panel { Dock = DockStyle.Top, Height = 36 };
            txtServerDir.Text = serverDir;
            txtServerDir.ReadOnly = true;
            txtServerDir.Width = 560; txtServerDir.Location = new System.Drawing.Point(8, 7);
            btnBrowse.Text = "浏览..."; btnBrowse.Location = new System.Drawing.Point(576, 4);
            btnBrowse.Click += (s, e) =>
            {
                var d = new FolderBrowserDialog { SelectedPath = serverDir, Description = "选择 LMP 服务端目录 (含 Server.exe)" };
                if (d.ShowDialog() == DialogResult.OK) { serverDir = d.SelectedPath; txtServerDir.Text = serverDir; LoadConfigFiles(); LoadModControl(); UpdateStatus(); }
            };
            top.Controls.AddRange(new Control[] { new Label { Text = "服务端目录:", Location = new System.Drawing.Point(8, 10) }, txtServerDir, btnBrowse });
            Controls.Add(top);
        }

        private TabPage MakeServerTab()
        {
            var tp = new TabPage("服务器");
            btnStart.Text = "启动服务器"; btnStart.Width = 120; btnStart.Location = new System.Drawing.Point(12, 16);
            btnStart.Click += (s, e) => StartServer();
            btnStop.Text = "停止服务器"; btnStop.Width = 120; btnStop.Location = new System.Drawing.Point(144, 16);
            btnStop.Click += (s, e) => StopServer();
            lblStatus.Location = new System.Drawing.Point(12, 60); lblStatus.AutoSize = true; lblStatus.Text = "状态: -";
            lblPid.Location = new System.Drawing.Point(12, 84); lblPid.AutoSize = true; lblPid.Text = "PID: -";
            lblUptime.Location = new System.Drawing.Point(12, 108); lblUptime.AutoSize = true; lblUptime.Text = "运行时长: -";
            lblPort.Location = new System.Drawing.Point(12, 132); lblPort.AutoSize = true; lblPort.Text = "端口: -";
            tp.Controls.AddRange(new Control[] { btnStart, btnStop, lblStatus, lblPid, lblUptime, lblPort });
            return tp;
        }

        private TabPage MakeConfigTab()
        {
            var tp = new TabPage("配置");
            cboConfig.Location = new System.Drawing.Point(12, 12); cboConfig.Width = 300;
            cboConfig.SelectedIndexChanged += (s, e) => LoadConfigText();
            btnSaveConfig.Text = "保存"; btnSaveConfig.Width = 80; btnSaveConfig.Location = new System.Drawing.Point(320, 11);
            btnSaveConfig.Click += (s, e) => SaveConfigText();
            txtConfig.Multiline = true; txtConfig.ScrollBars = ScrollBars.Both;
            txtConfig.Location = new System.Drawing.Point(12, 44); txtConfig.Size = new System.Drawing.Size(770, 480);
            txtConfig.Font = new System.Drawing.Font("Consolas", 9.5f);
            tp.Controls.AddRange(new Control[] { cboConfig, btnSaveConfig, txtConfig });
            return tp;
        }

        private TabPage MakeModTab()
        {
            var tp = new TabPage("Mod 白名单");
            chkAllowNonListed.Location = new System.Drawing.Point(12, 12); chkAllowNonListed.AutoSize = true;
            chkAllowNonListed.Text = "允许未列出的插件 (AllowNonListedPlugins)";
            lstDll.Location = new System.Drawing.Point(12, 44); lstDll.Size = new System.Drawing.Size(500, 420);
            txtAddDll.Location = new System.Drawing.Point(12, 472); txtAddDll.Size = new System.Drawing.Size(400, 23);
            txtAddDll.PlaceholderText = "输入 DLL 相对路径, 如 MyMod/Plugins/mymod.dll";
            btnAddDll.Text = "添加"; btnAddDll.Location = new System.Drawing.Point(420, 470);
            btnAddDll.Click += (s, e) => AddDll();
            btnRemoveDll.Text = "移除选中"; btnRemoveDll.Location = new System.Drawing.Point(510, 470);
            btnRemoveDll.Click += (s, e) => RemoveDll();
            btnSaveMod.Text = "保存白名单"; btnSaveMod.Location = new System.Drawing.Point(12, 502);
            btnSaveMod.Click += (s, e) => SaveModControl();
            tp.Controls.AddRange(new Control[] { chkAllowNonListed, lstDll, txtAddDll, btnAddDll, btnRemoveDll, btnSaveMod });
            return tp;
        }

        private TabPage MakeLogTab()
        {
            var tp = new TabPage("日志");
            btnRefreshLog.Text = "刷新"; btnRefreshLog.Location = new System.Drawing.Point(12, 12);
            btnRefreshLog.Click += (s, e) => LoadLog();
            txtLog.Multiline = true; txtLog.ScrollBars = ScrollBars.Both; txtLog.ReadOnly = true;
            txtLog.Location = new System.Drawing.Point(12, 44); txtLog.Size = new System.Drawing.Size(770, 480);
            txtLog.Font = new System.Drawing.Font("Consolas", 9f);
            tp.Controls.AddRange(new Control[] { btnRefreshLog, txtLog });
            return tp;
        }

        private TabPage MakeResetTab()
        {
            var tp = new TabPage("清档重开");
            btnReset.Text = "备份并清空 Universe 后重启"; btnReset.Location = new System.Drawing.Point(12, 16); btnReset.Width = 280;
            btnReset.Click += (s, e) => ResetUniverse();
            lblResetHint.Location = new System.Drawing.Point(12, 56); lblResetHint.AutoSize = true;
            lblResetHint.Text = "会把 Universe 改名备份为 _universe_backup_时间戳, 服务端将重建全新存档。";
            tp.Controls.AddRange(new Control[] { btnReset, lblResetHint });
            return tp;
        }

        // ---------- 服务器控制 ----------
        private void StartServer()
        {
            var exe = Path.Combine(serverDir, "Server.exe");
            if (!File.Exists(exe)) { MessageBox.Show("未找到 Server.exe: " + exe, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (IsServerRunning())
            {
                var r = MessageBox.Show("服务端已在运行, 是否重新启动?", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r == DialogResult.No) return;
                StopServer();
            }
            var psi = new ProcessStartInfo(exe) { WorkingDirectory = serverDir, UseShellExecute = false };
            serverProcess = Process.Start(psi);
            startTime = DateTime.Now;
            UpdateStatus();
        }

        private void StopServer()
        {
            bool killed = false;
            if (serverProcess != null && !serverProcess.HasExited)
            {
                try { serverProcess.Kill(); killed = true; } catch { }
            }
            // 兜底: 终止本目录下的 Server.exe 进程
            foreach (var p in Process.GetProcessesByName("Server"))
            {
                try
                {
                    if (p.MainModule != null && p.MainModule.FileName.StartsWith(serverDir, StringComparison.OrdinalIgnoreCase))
                    { p.Kill(); killed = true; }
                }
                catch { }
            }
            if (killed) { serverProcess = null; startTime = null; }
            UpdateStatus();
        }

        private bool IsServerRunning()
        {
            if (serverProcess != null && !serverProcess.HasExited) return true;
            foreach (var p in Process.GetProcessesByName("Server"))
            {
                try
                {
                    if (p.MainModule != null && p.MainModule.FileName.StartsWith(serverDir, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { }
            }
            return false;
        }

        private void UpdateStatus()
        {
            bool running = IsServerRunning();
            lblStatus.Text = "状态: " + (running ? "运行中" : "已停止");
            int pid = 0;
            if (serverProcess != null && !serverProcess.HasExited) pid = serverProcess.Id;
            else
            {
                foreach (var p in Process.GetProcessesByName("Server"))
                {
                    try { if (p.MainModule != null && p.MainModule.FileName.StartsWith(serverDir, StringComparison.OrdinalIgnoreCase)) { pid = p.Id; break; } } catch { }
                }
            }
            lblPid.Text = "PID: " + (pid > 0 ? pid.ToString() : "-");
            lblUptime.Text = "运行时长: " + (running && startTime != null ? (DateTime.Now - startTime.Value).ToString(@"hh\:mm\:ss") : (pid > 0 ? "(外部进程)" : "-"));
            lblPort.Text = "端口: " + ReadPort();
        }

        private string ReadPort()
        {
            var f = Path.Combine(serverDir, "Config", "ConnectionSettings.xml");
            if (!File.Exists(f)) return "-";
            try
            {
                var doc = LoadXmlSafe(f);
                var n = doc.SelectSingleNode("//Port") ?? doc.SelectSingleNode("//*[local-name()='Port']");
                return n?.InnerText ?? "-";
            }
            catch { return "-"; }
        }

        // ---------- 配置编辑 ----------
        private void LoadConfigFiles()
        {
            cboConfig.Items.Clear();
            var cfg = Path.Combine(serverDir, "Config");
            if (Directory.Exists(cfg))
            {
                foreach (var f in Directory.GetFiles(cfg, "*.xml").OrderBy(x => x))
                    cboConfig.Items.Add(Path.GetFileName(f));
            }
            if (cboConfig.Items.Count > 0) cboConfig.SelectedIndex = 0;
        }

        private void LoadConfigText()
        {
            if (cboConfig.SelectedItem == null) { txtConfig.Text = ""; return; }
            var f = Path.Combine(serverDir, "Config", cboConfig.SelectedItem.ToString()!);
            txtConfig.Text = ReadDetect(f).text;
        }

        private void SaveConfigText()
        {
            if (cboConfig.SelectedItem == null) return;
            var f = Path.Combine(serverDir, "Config", cboConfig.SelectedItem.ToString()!);
            var (_, enc) = ReadDetect(f);
            WriteDetect(f, txtConfig.Text, enc);
            MessageBox.Show("已保存: " + cboConfig.SelectedItem, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------- Mod 白名单 ----------
        private void LoadModControl()
        {
            var f = Path.Combine(serverDir, "Config", "LMPModControl.xml");
            lstDll.Items.Clear();
            if (!File.Exists(f)) { chkAllowNonListed.Checked = false; return; }
            try
            {
                var doc = LoadXmlSafe(f);
                var allow = doc.SelectSingleNode("//*[local-name()='AllowNonListedPlugins']");
                chkAllowNonListed.Checked = allow != null && allow.InnerText.Trim().ToLower() == "true";
                var files = doc.SelectNodes("//*[local-name()='OptionalPlugins']/*[local-name()='DllFile']");
                if (files != null)
                    foreach (XmlNode n in files)
                    {
                        var fp = n.SelectSingleNode("*[local-name()='FilePath']");
                        if (fp != null) lstDll.Items.Add(fp.InnerText);
                    }
            }
            catch (Exception ex) { MessageBox.Show("读取白名单失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void AddDll()
        {
            var v = txtAddDll.Text.Trim();
            if (string.IsNullOrEmpty(v)) return;
            if (!lstDll.Items.Contains(v)) lstDll.Items.Add(v);
            txtAddDll.Text = "";
        }

        private void RemoveDll()
        {
            if (lstDll.SelectedItem != null) lstDll.Items.Remove(lstDll.SelectedItem);
        }

        private void SaveModControl()
        {
            var f = Path.Combine(serverDir, "Config", "LMPModControl.xml");
            if (!File.Exists(f)) { MessageBox.Show("未找到 LMPModControl.xml", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            try
            {
                var doc = LoadXmlSafe(f);
                var allow = doc.SelectSingleNode("//*[local-name()='AllowNonListedPlugins']");
                if (allow != null) allow.InnerText = chkAllowNonListed.Checked ? "true" : "false";
                var opt = doc.SelectSingleNode("//*[local-name()='OptionalPlugins']");
                if (opt != null)
                {
                    opt.RemoveAll();
                    foreach (var item in lstDll.Items)
                    {
                        var dn = doc.CreateElement("DllFile");
                        var t = doc.CreateElement("Text"); t.InnerText = item.ToString();
                        var fp = doc.CreateElement("FilePath"); fp.InnerText = item.ToString();
                        var sha = doc.CreateElement("Sha"); sha.InnerText = "";
                        dn.AppendChild(t); dn.AppendChild(fp); dn.AppendChild(sha);
                        opt.AppendChild(dn);
                    }
                }
                var (_, enc) = ReadDetect(f);
                WriteDetect(f, doc.OuterXml, enc);
                MessageBox.Show("白名单已保存", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show("保存失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ---------- 日志 ----------
        private void LoadLog()
        {
            var logs = Path.Combine(serverDir, "logs");
            if (!Directory.Exists(logs)) { txtLog.Text = "(无日志目录)"; return; }
            var file = Directory.GetFiles(logs, "lmpserver_*.log").OrderByDescending(x => x).FirstOrDefault();
            if (file == null) { txtLog.Text = "(无日志)"; return; }
            try { txtLog.Text = File.ReadAllText(file, Encoding.UTF8); txtLog.SelectionStart = txtLog.Text.Length; txtLog.ScrollToCaret(); }
            catch (Exception ex) { txtLog.Text = "读取失败: " + ex.Message; }
        }

        // ---------- 清档重开 ----------
        private void ResetUniverse()
        {
            var uni = Path.Combine(serverDir, "Universe");
            if (!Directory.Exists(uni)) { MessageBox.Show("未找到 Universe 目录", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var r = MessageBox.Show("将停止服务端, 备份并清空 Universe, 然后重启。确定继续?", "确认清档", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            StopServer();
            var backup = Path.Combine(serverDir, "_universe_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmm"));
            try { Directory.Move(uni, backup); }
            catch (Exception ex) { MessageBox.Show("备份失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            StartServer();
            MessageBox.Show("已完成。旧档备份于: " + backup, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------- 编码安全的读写 (LMP 配置声明 utf-16 实际 utf-8) ----------
        private static XmlDocument LoadXmlSafe(string path)
        {
            var (text, _) = ReadDetect(path);
            if (text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            {
                int end = text.IndexOf("?>");
                if (end > 0)
                {
                    var decl = text.Substring(0, end + 2);
                    decl = decl.Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"", StringComparison.OrdinalIgnoreCase)
                               .Replace("encoding='utf-16'", "encoding='utf-8'", StringComparison.OrdinalIgnoreCase);
                    text = decl + text.Substring(end + 2);
                }
            }
            var doc = new XmlDocument();
            doc.LoadXml(text);
            return doc;
        }

        private static (string text, Encoding enc) ReadDetect(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
                return (Encoding.Unicode.GetString(bytes), Encoding.Unicode);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), Encoding.UTF8);
            return (Encoding.UTF8.GetString(bytes), Encoding.UTF8);
        }

        private static void WriteDetect(string path, string text, Encoding enc)
        {
            byte[] bom = enc == Encoding.Unicode ? new byte[] { 0xFF, 0xFE } : Array.Empty<byte>();
            var data = bom.Concat(enc.GetBytes(text)).ToArray();
            File.WriteAllBytes(path, data);
        }
    }
}
