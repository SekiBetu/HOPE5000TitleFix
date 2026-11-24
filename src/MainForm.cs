using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace HOPE5000TitleFix
{
    public sealed class MainForm : Form
    {
        private readonly string _initialTarget;
        private readonly Color _headerColor = Color.FromArgb(31, 52, 76);
        private readonly Color _accentColor = Color.FromArgb(37, 99, 145);
        private readonly Color _normalButtonColor = Color.FromArgb(235, 240, 246);
        private readonly Color _normalButtonTextColor = Color.FromArgb(31, 58, 95);
        private readonly Color _patchButtonColor = Color.FromArgb(31, 122, 77);
        private readonly Color _restoreButtonColor = Color.FromArgb(156, 72, 49);

        private TextBox _targetBox;
        private TextBox _titleBox;
        private TextBox _logBox;
        private Label _statusLabel;
        private Button _browseButton;
        private Button _autoDetectButton;
        private Button _inspectButton;
        private Button _patchButton;
        private Button _restoreButton;
        private bool _busy;

        public MainForm(string initialTarget)
        {
            _initialTarget = initialTarget;
            InitializeWindow();
            BuildLayout();
            ConfigureDragAndDrop();
            if (!string.IsNullOrEmpty(_initialTarget))
            {
                _targetBox.Text = _initialTarget;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Log("工具已启动。请先确认目标文件，再点击“检查状态”。");

            if (string.IsNullOrWhiteSpace(_targetBox.Text))
            {
                TryAutoDetect(false);
            }
            else
            {
                Log("目标文件：" + _targetBox.Text);
            }

            if (!string.IsNullOrWhiteSpace(_targetBox.Text))
            {
                SetStatus("准备就绪。操作前请完全退出 HOPE5000。", _headerColor);
            }
        }

        private void InitializeWindow()
        {
            Text = "HOPE5000 标题热修复 1.0.0";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(820, 690);
            MinimumSize = new Size(740, 610);
            BackColor = Color.FromArgb(247, 249, 252);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;
        }

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 7;
            root.Padding = new Padding(18, 0, 18, 10);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildTargetGroup(), 0, 1);
            root.Controls.Add(BuildTitleGroup(), 0, 2);
            root.Controls.Add(BuildButtonBar(), 0, 3);
            root.Controls.Add(BuildStatusBar(), 0, 4);
            root.Controls.Add(BuildLogGroup(), 0, 5);
            root.Controls.Add(BuildFooter(), 0, 6);
        }

        private Control BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0, 0, 0, 7);
            header.BackColor = _headerColor;

            Label title = new Label();
            title.AutoSize = true;
            title.Location = new Point(22, 15);
            title.Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold);
            title.ForeColor = Color.White;
            title.Text = "HOPE5000 标题热修复";
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.AutoSize = true;
            subtitle.Location = new Point(24, 54);
            subtitle.Font = new Font("Microsoft YaHei UI", 9F);
            subtitle.ForeColor = Color.FromArgb(205, 219, 233);
            subtitle.Text = "修复自定义烧录子窗口闪退 · 自定义窗口标题 · 自动备份与还原";
            header.Controls.Add(subtitle);

            Label version = new Label();
            version.AutoSize = true;
            version.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            version.Location = new Point(header.Width - 90, 22);
            version.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            version.ForeColor = Color.FromArgb(160, 190, 215);
            version.Text = "v1.0.0";
            header.Controls.Add(version);
            header.Resize += delegate
            {
                version.Left = header.ClientSize.Width - version.Width - 22;
            };

            return header;
        }

        private Control BuildTargetGroup()
        {
            GroupBox group = new GroupBox();
            group.Text = "目标程序";
            group.Dock = DockStyle.Fill;
            group.Margin = new Padding(0, 0, 0, 6);
            group.Padding = new Padding(10, 10, 10, 7);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 3;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            group.Controls.Add(layout);

            _targetBox = new TextBox();
            _targetBox.Dock = DockStyle.Fill;
            _targetBox.Margin = new Padding(2, 5, 6, 3);
            _targetBox.Font = new Font("Microsoft YaHei UI", 9F);
            _targetBox.AccessibleName = "HOPE5000 可执行文件路径";
            layout.Controls.Add(_targetBox, 0, 0);

            _browseButton = CreateButton("浏览...", _normalButtonColor, _normalButtonTextColor);
            _browseButton.Dock = DockStyle.Fill;
            _browseButton.Margin = new Padding(2, 3, 2, 3);
            _browseButton.Click += OnBrowseClick;
            layout.Controls.Add(_browseButton, 1, 0);

            _autoDetectButton = CreateButton("自动查找", _normalButtonColor, _normalButtonTextColor);
            _autoDetectButton.Dock = DockStyle.Fill;
            _autoDetectButton.Margin = new Padding(2, 3, 2, 3);
            _autoDetectButton.Click += OnAutoDetectClick;
            layout.Controls.Add(_autoDetectButton, 2, 0);

            return group;
        }

        private Control BuildTitleGroup()
        {
            GroupBox group = new GroupBox();
            group.Text = "自定义窗口标题";
            group.Dock = DockStyle.Fill;
            group.Margin = new Padding(0, 0, 0, 6);
            group.Padding = new Padding(10, 10, 10, 7);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            group.Controls.Add(layout);

            _titleBox = new TextBox();
            _titleBox.Dock = DockStyle.Fill;
            _titleBox.Margin = new Padding(2, 5, 8, 3);
            _titleBox.Font = new Font("Microsoft YaHei UI", 9F);
            _titleBox.Text = "HOPE5000";
            _titleBox.AccessibleName = "新的窗口标题";
            layout.Controls.Add(_titleBox, 0, 0);

            Label hint = new Label();
            hint.Dock = DockStyle.Fill;
            hint.Margin = new Padding(0, 0, 0, 0);
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.ForeColor = Color.FromArgb(86, 102, 119);
            hint.Text = "建议保留 HOPE5000，也可以改为任意名称。";
            layout.Controls.Add(hint, 1, 0);

            return group;
        }

        private Control BuildButtonBar()
        {
            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.FlowDirection = FlowDirection.LeftToRight;
            panel.WrapContents = false;
            panel.Margin = new Padding(0, 1, 0, 5);
            panel.Padding = new Padding(0, 2, 0, 0);

            _inspectButton = CreateButton("检查状态", _normalButtonColor, _normalButtonTextColor);
            _inspectButton.Size = new Size(126, 36);
            _inspectButton.Click += OnInspectClick;
            panel.Controls.Add(_inspectButton);

            _patchButton = CreateButton("打补丁", _patchButtonColor, Color.White);
            _patchButton.Size = new Size(142, 36);
            _patchButton.Click += OnPatchClick;
            panel.Controls.Add(_patchButton);

            _restoreButton = CreateButton("还原原始文件", _restoreButtonColor, Color.White);
            _restoreButton.Size = new Size(154, 36);
            _restoreButton.Click += OnRestoreClick;
            panel.Controls.Add(_restoreButton);

            return panel;
        }

        private Control BuildStatusBar()
        {
            _statusLabel = new Label();
            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.Margin = new Padding(0, 0, 0, 5);
            _statusLabel.Padding = new Padding(10, 0, 10, 0);
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.BackColor = Color.FromArgb(233, 239, 246);
            _statusLabel.ForeColor = _headerColor;
            _statusLabel.Text = "准备就绪。操作前请完全退出 HOPE5000。";
            return _statusLabel;
        }

        private Control BuildLogGroup()
        {
            GroupBox group = new GroupBox();
            group.Text = "操作日志";
            group.Dock = DockStyle.Fill;
            group.Margin = new Padding(0, 2, 0, 5);
            group.Padding = new Padding(9, 9, 9, 9);

            _logBox = new TextBox();
            _logBox.Dock = DockStyle.Fill;
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.ScrollBars = ScrollBars.Both;
            _logBox.WordWrap = false;
            _logBox.BackColor = Color.FromArgb(252, 253, 254);
            _logBox.ForeColor = Color.FromArgb(45, 55, 65);
            _logBox.Font = new Font("Consolas", 9F);
            _logBox.BorderStyle = BorderStyle.FixedSingle;
            _logBox.AccessibleName = "操作日志";
            group.Controls.Add(_logBox);
            return group;
        }

        private Control BuildFooter()
        {
            Label footer = new Label();
            footer.Dock = DockStyle.Fill;
            footer.Margin = new Padding(0, 2, 0, 0);
            footer.TextAlign = ContentAlignment.MiddleLeft;
            footer.Font = new Font("Microsoft YaHei UI", 8F);
            footer.ForeColor = Color.FromArgb(103, 113, 125);
            footer.Text =
                "非合泰（Holtek）官方工具，与厂商无关联。修改后的程序可能触发安全软件提示，也可能不受厂商支持。\r\n" +
                "首次打补丁会自动创建 .original.bak；还原不会删除该备份。";
            return footer;
        }

        private Button CreateButton(string text, Color backColor, Color foreColor)
        {
            Button button = new Button();
            button.Text = text;
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(180, 192, 205);
            button.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.Margin = new Padding(0, 0, 8, 0);
            return button;
        }

        private void ConfigureDragAndDrop()
        {
            AllowDrop = true;
            _targetBox.AllowDrop = true;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            _targetBox.DragEnter += OnDragEnter;
            _targetBox.DragDrop += OnDragDrop;
        }

        private void OnBrowseClick(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 HOPE5000.exe";
                dialog.Filter = "HOPE5000 (HOPE5000.exe)|HOPE5000.exe|可执行文件 (*.exe)|*.exe";
                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;

                try
                {
                    string currentPath = _targetBox.Text.Trim();
                    if (currentPath.Length > 0)
                    {
                        string directory = Path.GetDirectoryName(currentPath);
                        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                        {
                            dialog.InitialDirectory = directory;
                        }
                    }
                }
                catch
                {
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _targetBox.Text = dialog.FileName;
                    Log("已选择目标：" + dialog.FileName);
                }
            }
        }

        private void OnAutoDetectClick(object sender, EventArgs e)
        {
            TryAutoDetect(true);
        }

        private void TryAutoDetect(bool showMessage)
        {
            string found = PathDetector.FindDefault();
            if (string.IsNullOrEmpty(found))
            {
                Log("未在常见位置找到 HOPE5000.exe，请手动浏览选择。");
                if (showMessage)
                {
                    MessageBox.Show(
                        "未在常见位置找到 HOPE5000.exe。\r\n\r\n" +
                        "请点击“浏览...”手动选择，也可以把 HOPE5000.exe 拖到窗口中。",
                        "未找到目标文件",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                return;
            }

            _targetBox.Text = found;
            Log("已自动定位：" + found);
            SetStatus("已找到 HOPE5000。建议先点击“检查状态”。", _accentColor);
        }

        private void OnInspectClick(object sender, EventArgs e)
        {
            RunOperation(delegate
            {
                Inspection inspection = Patcher.Inspect(_targetBox.Text);
                AppendInspection(inspection);
            });
        }

        private void OnPatchClick(object sender, EventArgs e)
        {
            RunOperation(delegate
            {
                string title = _titleBox.Text;
                Patcher.ValidateTitle(title);
                string path = FileSafety.NormalizePath(_targetBox.Text);

                string confirmation =
                    "确定要为以下程序打补丁吗？\r\n\r\n" +
                    "目标：" + path + "\r\n" +
                    "新窗口标题：“" + title + "”\r\n\r\n" +
                    "首次操作会创建原始备份：\r\n" + path + ".original.bak\r\n\r\n" +
                    "请确认 HOPE5000 已完全退出。";
                DialogResult answer = MessageBox.Show(
                    this, confirmation, "确认打补丁",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes)
                {
                    Log("已取消打补丁。");
                    return;
                }

                PatchResult result = Patcher.Patch(path, title);
                Log("");
                Log("=== 打补丁完成 ===");
                Log("目标：" + result.TargetPath);
                Log("原标题：" + DisplayTitle(result.OldTitle));
                Log("新标题：“" + result.NewTitle + "”");
                Log("备份：" + result.BackupPath +
                    (result.BackupCreated ? "（本次新建）" : "（已存在）"));
                Log("原始 SHA256：" + result.OriginalSha256);
                Log("补丁 SHA256：" + result.PatchedSha256);
                for (int i = 0; i < result.Warnings.Length; i++)
                {
                    Log("警告：" + result.Warnings[i]);
                }
                SetStatus("补丁完成。请启动 HOPE5000，测试自定义烧录窗口。",
                    _patchButtonColor);

                string message =
                    "补丁完成。\r\n\r\n请启动 HOPE5000，打开“自定义烧录/范围选择”窗口验证。\r\n\r\n" +
                    "如需恢复原状，请点击“还原原始文件”。";
                if (result.Warnings.Length > 0)
                {
                    message += "\r\n\r\n注意：" + string.Join("\r\n", result.Warnings);
                }
                MessageBox.Show(this, message, "补丁完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        private void OnRestoreClick(object sender, EventArgs e)
        {
            RunOperation(delegate
            {
                string path = FileSafety.NormalizePath(_targetBox.Text);
                string backupPath = path + ".original.bak";
                string confirmation =
                    "确定要还原原始文件吗？\r\n\r\n" +
                    "目标：" + path + "\r\n" +
                    "备份：" + backupPath + "\r\n\r\n" +
                    "还原会覆盖当前 HOPE5000.exe，但不会删除备份。";
                DialogResult answer = MessageBox.Show(
                    this, confirmation, "确认还原",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes)
                {
                    Log("已取消还原。");
                    return;
                }

                RestoreResult result = Patcher.Restore(path);
                Log("");
                Log("=== 还原完成 ===");
                Log("目标：" + result.TargetPath);
                Log("来源：" + result.RestoredFrom);
                Log("原始 SHA256：" + result.RestoredSha256);
                for (int i = 0; i < result.Warnings.Length; i++)
                {
                    Log("警告：" + result.Warnings[i]);
                }
                SetStatus("已还原为原始状态。", _restoreButtonColor);
                MessageBox.Show(
                    this,
                    "还原完成。目标文件的 SHA256 与原始备份一致。",
                    "还原完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            });
        }

        private void RunOperation(Action operation)
        {
            if (_busy)
            {
                return;
            }

            SetBusy(true);
            try
            {
                operation();
            }
            catch (Exception ex)
            {
                Log("");
                Log("[操作失败] " + ex.Message);
                SetStatus("操作失败：" + ex.Message, _restoreButtonColor);
                MessageBox.Show(
                    this, ex.Message, "操作失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            _browseButton.Enabled = !busy;
            _autoDetectButton.Enabled = !busy;
            _inspectButton.Enabled = !busy;
            _patchButton.Enabled = !busy;
            _restoreButton.Enabled = !busy;
            _titleBox.Enabled = !busy;
            _targetBox.Enabled = !busy;
            UseWaitCursor = busy;
        }

        private void AppendInspection(Inspection inspection)
        {
            Log("");
            Log("=== 状态检查 ===");
            Log("文件：" + inspection.ExePath);
            Log("程序集：" + inspection.AssemblyFullName);
            Log("当前标题：" + DisplayTitle(inspection.CurrentTitle));
            Log("标题指令：" + string.Format(
                CultureInfo.InvariantCulture, "IL_{0:X4}", inspection.TitleOffset));
            Log("当前 SHA256：" + inspection.ExeSha256);
            Log("备份存在：" + (inspection.BackupExists ? "是" : "否"));
            Log("备份路径：" + inspection.BackupPath);
            if (inspection.BackupExists)
            {
                if (inspection.BackupError != null)
                {
                    Log("备份状态：无法验证 - " + inspection.BackupError);
                }
                else
                {
                    Log("备份标题：" + DisplayTitle(inspection.BackupTitle));
                    Log("备份 SHA256：" + inspection.BackupSha256);
                }
            }
            Log("补丁说明文件：" +
                (inspection.MetadataExists ? inspection.MetadataPath : "无"));

            if (inspection.HasCustomTitle)
            {
                SetStatus("已打补丁，当前窗口标题：“" +
                    inspection.CurrentTitle + "”。", _patchButtonColor);
                _titleBox.Text = inspection.CurrentTitle;
            }
            else
            {
                SetStatus("当前为原始状态（空标题），可以打补丁。", _accentColor);
            }
        }

        private void Log(string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + "\r\n";
            _logBox.AppendText(line);
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
        }

        private void SetStatus(string message, Color foreColor)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = foreColor;
        }

        private static string DisplayTitle(string title)
        {
            return string.IsNullOrEmpty(title)
                ? "（空，原始状态）"
                : "“" + title + "”";
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length == 1 &&
                    string.Equals(
                        Path.GetExtension(files[0]), ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length != 1)
            {
                return;
            }

            if (!string.Equals(
                Path.GetExtension(files[0]), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                Log("只能拖入 .exe 文件。");
                return;
            }

            _targetBox.Text = files[0];
            Log("已拖入目标：" + files[0]);
            SetStatus("已选择目标。建议先点击“检查状态”。", _accentColor);
        }
    }
}
