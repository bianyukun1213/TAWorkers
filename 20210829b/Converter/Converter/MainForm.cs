using menelabs.core;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AutoUpdaterDotNET;
using NLog.Config;

namespace Converter
{
    public partial class MainForm : Form
    {
        /*
         * 日志记录器，通常使用 logger.Info()，
         * 调试记录等级使用 logger.Debug()，
         * 警告记录等级使用 logger.Warn()，
         * 错误记录等级使用 logger.Error()，
         * 严重错误记录等级使用 logger.Fatal()。
         */
        private static Logger logger;
        private FileSystemSafeWatcher safeWatcher; // 文件监视器。
        private string input; // 输入目录。
        private string output; // 输出目录。

#if DEBUG
        // 使用预处理器指令，如果程序运行在 Debug 配置下，DEBUG 常量就为 true。DEBUG 常量影响检查更新功能。
        private const bool DEBUG = true;
#else
        private const bool DEBUG = false;
#endif

        public MainForm()
        {
            InitializeComponent();
        }

        #region 主窗口加载事件
        private void MainForm_Load(object sender, EventArgs e)
        {
            Text += " " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); // 设置主窗口标题为 标题 + 版本号。
            logger = LogManager.GetCurrentClassLogger(); // 获取当前类的日志记录器。
            if (DEBUG) // 如果程序运行在 Debug 配置下，设置日志记录器在主窗口富文本框的最低记录等级为 Trace。
            {
                LoggingConfiguration lc = LogManager.Configuration;
                LoggingRule lr = lc.LoggingRules.FirstOrDefault(
                    r => r.Targets.Any(
                        t => t.Name == "logRichTextBox"
                    )
                );
                if (lr != null)
                    lc.LoggingRules.Remove(lr);
                lc.AddRule(LogLevel.Trace, LogLevel.Fatal, "logRichTextBox");
                LogManager.ReconfigExistingLoggers();
                logger.Debug("程序运行在 Debug 配置下。");
                Text += " [Debug]";
            }
            logger.Info("欢迎使用 Converter {0}！", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
            CheckForUpdates(false); // 检查更新，传入 false 以避免弹出窗口。
            safeWatcher = new FileSystemSafeWatcher
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite // 使文件监视器监测文件改动名和修改时间。
            };
            safeWatcher.Changed += OnChanged; // 注册文件监视器的文件变化事件。
            safeWatcher.Created += OnCreated; // 注册文件监视器的文件创建事件。
            safeWatcher.Deleted += OnDeleted; // 注册文件监视器的文件删除事件。
            safeWatcher.Renamed += OnRenamed; // 注册文件监视器的文件重命名事件。
            safeWatcher.Error += OnError; // 注册文件监视器的错误事件。
            safeWatcher.Filter = "*.txt"; // 使文件监视器只监视 .txt 文件。
            safeWatcher.IncludeSubdirectories = false; // 使文件监视器忽略下层目录。
            string inputDir = Properties.Settings.Default.InputDirectory; // 从设置中读取输入目录。
            string outputDir = Properties.Settings.Default.OutputDirectory; // 从设置中读取输出目录。
            if (Directory.Exists(inputDir)) // 检查输入目录是否存在。
            {
                input = inputDir;
                logger.Info("读取输入目录为 {0}。", input);
            }
            else
            {
                input = "";
                string text = "输入目录不存在，请重新指定。";
                logger.Warn(text);
                MessageBox.Show(text, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (Directory.Exists(outputDir)) // 检查输出目录是否存在。
            {
                output = outputDir;
                logger.Info("读取输出目录为 {0}。", output);
            }
            else
            {
                output = "";
                string text = "输出目录不存在，请重新指定。";
                logger.Warn(text);
                MessageBox.Show(text, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            textBoxInput.Text = input; // 使输入目录显示在文本框内。
            textBoxOutput.Text = output; // 使输出目录显示在文本框内。
            SaveSettings(); // 保存设置。
        }
        #endregion

        #region 文件监视器的各种事件
        private void OnError(object sender, ErrorEventArgs e)
        {
            logger.Error(e.GetException(), "捕获到异常。");
            MessageBox.Show(e.GetException().Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            logger.Info("监测到 {0} 被重命名为 {1}。", e.OldName, e.Name);
            ProcessFile(e.Name); // 将文件名传给 ProcessFile，处理单个文件。
            DeleteFile(e.OldName); // 将旧文件名传给 DeleteFile，删除旧文件。
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            logger.Info("监测到 {0} 被删除。", e.Name);
            DeleteFile(e.Name);
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            logger.Info("监测到 {0} 被创建。", e.Name);
            ProcessFile(e.Name);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            logger.Info("监测到 {0} 被改动。", e.Name);
            ProcessFile(e.Name);
        }
        #endregion

        #region 输入目录或输出目录的 选择 按钮按下事件
        private void ButtonIOSelection_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK) // 弹出文件选择窗口。
            {
                if (((Button)sender).Name == "buttonInput") // 判断点击的按钮是输入目录的还是输出目录的。
                {
                    input = dialog.SelectedPath;
                    textBoxInput.Text = input;
                }
                else
                {
                    output = dialog.SelectedPath;
                    textBoxOutput.Text = output;
                }
                SaveSettings(); // 保存设置。
            }
        }
        #endregion

        #region 输入目录或输出目录的文本框拖拽事件
        private void TextBoxIO_DragDrop(object sender, DragEventArgs e)
        {
            TextBox tb = (TextBox)sender; // 将 sender 强制转换为 TextBox 类型。
            string path = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
            if (!Directory.Exists(path)) // 判断拖拽的对象是文件夹还是文件，如果是文件，就获取它所在的文件夹。
                path = Path.GetDirectoryName(path);
            if (tb.Name == "textBoxInput") // 判断拖拽到的文本框是输入目录的还是输出目录的。
            {
                input = path;
                tb.Text = input;
            }
            else
            {
                output = path;
                tb.Text = output;
            }
            SaveSettings(); // 保存设置。
        }

        private void TextBoxIO_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Link;
            else
                e.Effect = DragDropEffects.None;
        }
        #endregion

        #region 处理 按钮单击事件
        private void ButtonProcess_Click(object sender, EventArgs e)
        {
            ProcessFiles(); // 调用 ProcessFiles 批量处理 .txt 文件。
        }
        #endregion

        #region 主窗口大小改变事件
        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized) // 判断主窗口是否处于最小化状态。
            {
                Hide(); // 隐藏主窗口。
                notifyIcon1.Visible = true; // 显示托盘图标。
                notifyIcon1.ShowBalloonTip(5000, "通知", "程序已最小化到托盘，双击图标显示主窗口。", ToolTipIcon.Info); // 显示托盘图标通知。
                logger.Info("最小化到托盘。");
            }
        }
        #endregion

        #region 托盘图标双击事件
        private void NotifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            RecoverWindow(); // 调用 RecoverWindow，恢复窗口。
        }
        #endregion

        #region 主窗口关闭事件
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            notifyIcon1.Dispose(); // 释放托盘图标资源。
            safeWatcher.Dispose(); // 释放文件监视器资源。
        }
        #endregion

        #region 上下文菜单项目单击事件
        private void ContextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (e.ClickedItem.Name == "toolStripMenuItem1") // 如果点击的是 显示主窗口，就显示主窗口，否则退出程序。
                RecoverWindow();
            else
            {
                notifyIcon1.Dispose();
                safeWatcher.Dispose();
                Environment.Exit(0); // 完全退出程序。
            }
        }
        #endregion

        #region 恢复主窗口方法
        private void RecoverWindow()
        {
            Show(); // 显示主窗口。
            WindowState = FormWindowState.Normal; // 设置主窗口状态为 Normal。
            notifyIcon1.Visible = false; // 隐藏托盘图标。
        }
        #endregion

        #region 检查更新 按钮单击事件
        private void ButtonCheckForUpdates_Click(object sender, EventArgs e)
        {
            CheckForUpdates(); // 检查更新，不传入参数以显示窗口。
        }
        #endregion

        #region 关于 按钮单击事件
        private void ButtonAbout_Click(object sender, EventArgs e)
        {
            AboutBox1 ab = new AboutBox1(); // 创建 关于 页面。
            ab.Show(); // 显示 关于 页面。
        }
        #endregion

        #region 批量处理文件方法
        private void ProcessFiles()
        {
            StopWatching();
            if (Directory.Exists(input) && Directory.Exists(output)) // 输入目录与输出目录都不为空才继续。
            {
                try // 使用 try catch 捕获异常。
                {
                    logger.Info("开始处理文件，处理过程中请不要干预。");
                    int count = 0; // 计数用。
                    List<FileInfo> list = new DirectoryInfo(input).GetFiles("*.txt").ToList(); // list 存放 .txt 文件。
                    foreach (var item in list) // 遍历 list。
                    {
                        logger.Info("正在处理 {0}。", item.Name);
                        StringBuilder sb = new StringBuilder(File.ReadAllText(item.FullName)); // 读取 .txt 文件。涉及复杂的文本操作时，使用 StringBuilder 更好。
                        sb = sb.Replace("$\r\n", "\r\n").Replace("$", ","); // 替换掉 $。
                        Directory.CreateDirectory(output); // 创建输出目录。
                        File.WriteAllText(output + "\\" + item.Name.Replace(".txt", ".csv"), sb.ToString()); // 写 .csv 文件。
                        logger.Info("{0} 处理完成，已生成 {1}。", item.Name, item.Name.Replace(".txt", ".csv"));
                        count++; // 数字 + 1。
                        progressBar1.Value = 100 / list.Count * count; // 更新进度条。
                    }
                    progressBar1.Value = 0; // 进度条复位。
                    logger.Info("共处理 {0} 个文件。", count);
                    StartWatching();
                }
                catch (Exception ex)
                {
                    // 记录并显示异常。
                    logger.Error(ex, "捕获到异常。");
                    MessageBox.Show(ex.Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            if (!Directory.Exists(input))
            {
                input = "";
                textBoxInput.Text = input;
                string text = "输入目录不存在，请重新指定。";
                logger.Warn(text);
                MessageBox.Show(text, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (!Directory.Exists(output))
            {
                output = "";
                textBoxOutput.Text = output;
                string text = "输出目录不存在，请重新指定。";
                logger.Warn(text);
                MessageBox.Show(text, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        #endregion

        #region 单个文件处理方法
        private void ProcessFile(string fileName)
        {
            try
            {
                logger.Info("正在处理 {0}。", fileName);
                StringBuilder sb = new StringBuilder(File.ReadAllText(input + "\\" + fileName));
                sb = sb.Replace("$\r\n", "\r\n").Replace("$", ",");
                Directory.CreateDirectory(output);
                File.WriteAllText(output + "\\" + fileName.Replace(".txt", ".csv"), sb.ToString());
                logger.Info("{0} 处理完成，已生成 {1}。", fileName, fileName.Replace(".txt", ".csv"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "捕获到异常。");
                MessageBox.Show(ex.Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region 删除文件方法
        private void DeleteFile(string fileName)
        {
            try
            {
                string target = output + "\\" + fileName.Replace(".txt", ".csv");
                if (File.Exists(target)) // 如果输出目录中有旧的 .csv 文件，就删除它。
                    File.Delete(target);
                logger.Info("{0} 已同步删除。", fileName.Replace(".txt", ".csv"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "捕获到异常。");
                MessageBox.Show(ex.Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region 保存设置方法
        private void SaveSettings()
        {
            StopWatching();
            string tmpInput = input;
            string tmpOutput = output;
            if (!Directory.Exists(tmpInput))
                tmpInput = "空";
            if (!Directory.Exists(tmpOutput))
                tmpOutput = "空";
            logger.Info("保存输入目录为 {0}，输出目录为 {1}。", tmpInput, tmpOutput);
            Properties.Settings.Default.InputDirectory = input; // 保存输入目录到设置。
            Properties.Settings.Default.OutputDirectory = output; // 保存输出目录到设置。
            Properties.Settings.Default.Save(); // 保存设置。
            StartWatching();
        }
        #endregion

        #region 开始监测文件改动方法
        private void StartWatching()
        {
            safeWatcher.EnableRaisingEvents = false;
            logger.Debug("StartWatching 被调用，首先停止监测文件改动。");
            if (Directory.Exists(input) && Directory.Exists(output)) // 如果输入目录与输出目录都存在，就开始监测文件改动。
            {
                safeWatcher.Path = input;
                safeWatcher.EnableRaisingEvents = true;
                logger.Info("开始监测文件改动，监测过程中请确保输入目录本身不被重命名或删除，否则可能会导致程序抛出异常。");
            }
        }
        #endregion

        #region 停止监测文件改动方法
        private void StopWatching()
        {
            if (safeWatcher.EnableRaisingEvents)
            {
                safeWatcher.EnableRaisingEvents = false; // 停止监测文件改动。
                logger.Info("停止监测文件改动。");
            }
        }
        #endregion

        #region 检查更新方法
        private void CheckForUpdates(bool reportErrors = true)
        {
            logger.Info("使用 AutoUpdater.NET 检查更新。");
            AutoUpdater.ReportErrors = reportErrors;
            AutoUpdater.RunUpdateAsAdmin = false;
            if (DEBUG) // 根据配置使用不同的地址。
                AutoUpdater.Start("http://111.231.202.181/projects/converter/updates/config-debug.xml");
            else
                AutoUpdater.Start("http://111.231.202.181/projects/converter/updates/config.xml");
        }
        #endregion
    }
}
