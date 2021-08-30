using menelabs.core;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AutoUpdaterDotNET;
using System.Net;
using NLog.Config;

namespace Converter
{
    public partial class MainForm : Form
    {
        private static Logger logger;
        private FileSystemSafeWatcher safeWatcher;
        private string input;
        private string output;
#if DEBUG
        private const bool DEBUG = true;
#else
        private const bool DEBUG = false;
#endif
        public MainForm()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Text += " " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            logger = LogManager.GetCurrentClassLogger();
            if (DEBUG)
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
            CheckForUpdates(false);
            safeWatcher = new FileSystemSafeWatcher
            {
                NotifyFilter = NotifyFilters.FileName
                     | NotifyFilters.LastWrite
            };
            safeWatcher.Changed += OnChanged;
            safeWatcher.Created += OnCreated;
            safeWatcher.Deleted += OnDeleted;
            safeWatcher.Renamed += OnRenamed;
            safeWatcher.Error += OnError;
            safeWatcher.Filter = "*.txt";
            safeWatcher.IncludeSubdirectories = false;
            string inputDir = Properties.Settings.Default.InputDirectory;
            string outputDir = Properties.Settings.Default.OutputDirectory;
            if (Directory.Exists(inputDir))
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
            if (Directory.Exists(outputDir))
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
            textBoxInput.Text = input;
            textBoxOutput.Text = output;
            SaveSettings();
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            logger.Error(e.GetException(), "捕获到异常。");
            MessageBox.Show(e.GetException().Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            logger.Info("监测到 {0} 重命名为 {1}。", e.OldName, e.Name);
            ProcessFile(e.Name);
            DeleteFile(e.OldName);
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            logger.Info("监测到 {0} 删除。", e.Name);
            DeleteFile(e.Name);
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            logger.Info("监测到 {0} 创建。", e.Name);
            ProcessFile(e.Name);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            logger.Info("监测到 {0} 变化。", e.Name);
            ProcessFile(e.Name);
        }

        private void ButtonIOSelection_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (((Button)sender).Name == "buttonInput")
                {
                    input = dialog.SelectedPath;
                    textBoxInput.Text = input;
                }
                else
                {
                    output = dialog.SelectedPath;
                    textBoxOutput.Text = output;
                }
                SaveSettings();
            }
        }

        private void TextBoxIO_DragDrop(object sender, DragEventArgs e)
        {
            TextBox tb = ((TextBox)sender);
            if (tb.Name == "textBoxInput")
            {
                string path = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
                if (!Directory.Exists(path))
                    path = Path.GetDirectoryName(path);
                input = path;
                tb.Text = input;
            }
            else
            {
                string path = ((Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
                if (!Directory.Exists(path))
                    path = Path.GetDirectoryName(path);
                output = path;
                tb.Text = output;
            }
            SaveSettings();
        }

        private void TextBoxIO_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Link;
            else
                e.Effect = DragDropEffects.None;
        }

        private void ButtonProcess_Click(object sender, EventArgs e)
        {
            ProcessFiles();
        }

        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon1.Visible = true;
                notifyIcon1.ShowBalloonTip(5000, "通知", "程序已最小化到托盘，双击图标显示主窗口。", ToolTipIcon.Info);
                logger.Info("最小化到托盘。");
            }
        }

        private void NotifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            RecoverWindow();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            notifyIcon1.Dispose();
            safeWatcher.Dispose();
        }

        private void ContextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (e.ClickedItem.Name == "toolStripMenuItem1")
                RecoverWindow();
            else
            {
                notifyIcon1.Dispose();
                safeWatcher.Dispose();
                Environment.Exit(0);
            }
        }

        private void RecoverWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            notifyIcon1.Visible = false;
        }

        private void ButtonCheckForUpdates_Click(object sender, EventArgs e)
        {
            CheckForUpdates();
        }

        private void ButtonAbout_Click(object sender, EventArgs e)
        {
            AboutBox1 ab = new AboutBox1();
            ab.Show();
        }

        private void ProcessFiles()
        {
            if (!string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(output))
                try
                {
                    int count = 0;
                    List<FileInfo> list = new DirectoryInfo(input).GetFiles("*.txt").ToList();
                    foreach (var item in list)
                    {
                        logger.Info("正在处理 {0}。", item.Name);
                        StringBuilder sb = new StringBuilder(File.ReadAllText(item.FullName));
                        sb = sb.Replace("$\r\n", "\r\n").Replace("$", ",");
                        Directory.CreateDirectory(output);
                        File.WriteAllText(output + "\\" + item.Name.Replace(".txt", ".csv"), sb.ToString());
                        logger.Info("{0} 处理完成。", item.Name);
                        count++;
                        progressBar1.Value = 100 / list.Count * count;
                    }
                    progressBar1.Value = 0;
                    logger.Info("共处理 {0} 个文件。", count);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "捕获到异常。");
                    MessageBox.Show(ex.Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
        }

        private void ProcessFile(string fileName)
        {
            try
            {
                logger.Info("正在处理 {0}。", fileName);
                StringBuilder sb = new StringBuilder(File.ReadAllText(input + "\\" + fileName));
                sb = sb.Replace("$\r\n", "\r\n").Replace("$", ",");
                Directory.CreateDirectory(output);
                File.WriteAllText(output + "\\" + fileName.Replace(".txt", ".csv"), sb.ToString());
                logger.Info("{0} 处理完成。", fileName);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "捕获到异常。");
                MessageBox.Show(ex.Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteFile(string fileName)
        {
            try
            {
                string target = output + "\\" + fileName.Replace(".txt", ".csv");
                if (File.Exists(target))
                    File.Delete(target);
                logger.Info("{0} 已同步删除。", fileName.Replace(".txt", ".csv"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "捕获到异常。");
                MessageBox.Show(ex.Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveSettings()
        {
            safeWatcher.EnableRaisingEvents = false;
            Properties.Settings.Default.InputDirectory = input;
            Properties.Settings.Default.OutputDirectory = output;
            Properties.Settings.Default.Save();
            string tmp1 = input;
            string tmp2 = output;
            if (string.IsNullOrEmpty(tmp1))
                tmp1 = "空";
            if (string.IsNullOrEmpty(tmp2))
                tmp2 = "空";
            logger.Info("保存输入目录为 {0}，输出目录为 {1}。", tmp1, tmp2);
            if (!string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(output))
            {
                safeWatcher.Path = input;
                safeWatcher.EnableRaisingEvents = true;
                logger.Info("启动文件监视。");
            }
        }

        private void CheckForUpdates(bool reportErrors = true)
        {
            logger.Info("使用 AutoUpdater.NET 检查更新。");
            AutoUpdater.ReportErrors = reportErrors;
            AutoUpdater.RunUpdateAsAdmin = false;
            if (DEBUG)
                AutoUpdater.Start("http://111.231.202.181/projects/converter/updates/config-debug.xml");
            else
                AutoUpdater.Start("http://111.231.202.181/projects/converter/updates/config.xml");
        }
    }
}
