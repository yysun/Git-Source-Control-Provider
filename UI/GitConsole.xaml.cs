using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GitScc.DataServices;

namespace GitScc.UI
{
    /// <summary>
    /// Interaction logic for GitConsole.xaml
    /// </summary>
    public partial class GitConsole : UserControl
    {
        private GitFileStatusTracker tracker { get; set; }
        string prompt = ">";
        
        List<string> commandHistory = new List<string>();
        int commandIdx = -1;

        public GitConsole()
        {
            InitializeComponent();
        }

        public string GitExePath { get; set; }
        
        public string workingDirectory;
        public string WorkingDirectory 
        { 
            get { return workingDirectory; }
            set
            {
                if (string.Compare(workingDirectory, value) != 0)
                { 
                    workingDirectory = value;
                    prompt = string.Format("[{1}]>", workingDirectory,
                        GitIntellisenseHelper.GetPrompt(tracker));
                    this.richTextBox1.Document.Blocks.Clear();
                    ShowWaring();
                }
            }
        }

        #region keydown event
        private void richTextBox1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (lstOptions.Visibility == Visibility.Visible)
            {
                lstOptions.Focus();
                return;
            }

            if (!IsCaretPositionValid())
            {
                this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
                return;
            }

            if (e.Key == Key.Space)
            {
                var command = new TextRange(richTextBox1.CaretPosition.GetLineStartPosition(0),
                   richTextBox1.CaretPosition).Text;
                command = command.Substring(command.IndexOf(">") + 1).Trim();
                ShowOptions(command);
                return;
            }

            if (e.Key == Key.Enter)
            {
                var command = GetCommand();
                RunCommand(command);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                GetCommand(--commandIdx);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                GetCommand(++commandIdx);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ChangePrompt("", new SolidColorBrush(Colors.Black));
                lstOptions.Visibility = Visibility.Collapsed;
            }
            else if (e.Key == Key.Back)
            {
                var text = new TextRange(richTextBox1.CaretPosition.GetLineStartPosition(0),
                    richTextBox1.CaretPosition).Text;
                if (text.EndsWith(">")) e.Handled = true;
            }
        }

        private string GetCommand()
        {
            var command = new TextRange(
                richTextBox1.CaretPosition.GetLineStartPosition(0)
                .GetPositionAtOffset(prompt.Length + 1, LogicalDirection.Forward) ??
                richTextBox1.CaretPosition.GetLineStartPosition(0),
                richTextBox1.CaretPosition.GetLineStartPosition(1) ?? this.richTextBox1.CaretPosition.DocumentEnd).Text;
            command = command.Trim();
            return command;
        }

        private bool IsCaretPositionValid()
        {
            var text = new TextRange(richTextBox1.CaretPosition, richTextBox1.CaretPosition.DocumentEnd).Text;
            return !text.Contains(">");
        }

        private void ShowWaring()
        {
            WriteText(@"Welcome to Dragon console.", new SolidColorBrush(Colors.Black));
            WriteHelp();
            WriteText(@"WARNING: Git commands that require interactive inputs are not working in this console. E.g. git mergetool, git push/pull with http(s) that need to enter password are not supported. Please use Git Bash instead.

USE AT YOUR OWN RISK.
", new SolidColorBrush(Colors.Crimson));

            WritePrompt();
        }

        private void WriteHelp()
        {
            WriteText(@"Dragon console commands:

    cls:     clear the screen
    clear:   clear the screen
    dir:     windows shell command dir 
    git:     launch git bash
    git xxx: launch supported git command xxx
", new SolidColorBrush(Colors.Black));
            
        }

        #endregion

        #region run command and command history

        private void GetCommand(int idx)
        {
            if (commandHistory.Count > 0)
            {
                if (idx < 0) idx = 0;
                else if (idx > commandHistory.Count - 1) idx = commandHistory.Count - 1;
                var command = commandHistory[idx];
                commandIdx = idx;
                ChangePrompt(command, new SolidColorBrush(Colors.Black));
            }
        }

        bool isGit;
        int flag = 0;

        private void RunCommand(string command)
        {
            RunConsoleCommand(command);
        }

        private void RunConsoleCommand(string command)
        {
            isGit = true;
            if (!string.IsNullOrWhiteSpace(command) &&
               (commandHistory.Count == 0 || commandHistory.Last() != command))
            {
                commandHistory.Add(command);
                commandIdx = commandHistory.Count - 1;
            }

            if (!ProcessInternalCommand(command))
            {
                if (command == "git")
                {
                    //command = "/C \"\"" + GitExePath + "\"";
                    //GitBash.OpenGitBash(this.tracker.GitWorkingDirectory);
                    return;
                }
                else if (command.StartsWith("git "))
                {
                    command = command.Substring(4);
                    command = "/C \"\"" + GitExePath + "\" " + command + "\"";
                }
                else
                {
                    command = "/C " + command;
                    isGit = false;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe");
                startInfo.Arguments = command;
                //startInfo.RedirectStandardInput = true;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.ErrorDialog = false;
                startInfo.WorkingDirectory = WorkingDirectory;

                using (Process process = Process.Start(startInfo))
                using (ManualResetEvent mreOut = new ManualResetEvent(false), mreErr = new ManualResetEvent(false))
                {
                    flag = 0;

                    //new ReadOutput(process.StandardOutput, mreOut, (c) =>
                    //{
                    //    Action act = () =>
                    //    {
                    //        TextRange range = new TextRange(richTextBox1.Document.ContentEnd, richTextBox1.Document.ContentEnd);
                    //        range.Text = c.ToString();
                    //        this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
                    //        //this.richTextBox1.AppendText(c.ToString());
                    //    };
                    //    this.Dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
                    //});

                    process.OutputDataReceived += (o, e) =>
                    {
                        if (e.Data == null)
                        {
                            mreOut.Set();
                            Done();
                        }
                        else
                            WriteOutput(e.Data);
                    };
                    process.BeginOutputReadLine();

                    process.ErrorDataReceived += (o, e) =>
                    {
                        if (e.Data == null)
                        {
                            mreErr.Set();
                            Done();
                        }
                        else
                            WriteError(e.Data);
                    };
                    process.BeginErrorReadLine();

                    //string line = "git status";
                    ////while (input != null && null != (line = input.ReadLine())) 
                    //process.StandardInput.WriteLine(line);
                    //process.StandardInput.Close();

                    process.WaitForExit();
                    mreOut.WaitOne();
                    mreErr.WaitOne();

                    //var code = process.ExitCode;
                }
            }
        }

        //private void RunBashCommand(string command)
        //{
        //    if (!string.IsNullOrWhiteSpace(command) &&
        //       (commandHistory.Count == 0 || commandHistory.Last() != command))
        //    {
        //        commandHistory.Add(command);
        //        commandIdx = commandHistory.Count - 1;
        //    }

        //    if (!ProcessInternalCommand(command))
        //    {
        //        var GitBashPath = GitExePath.Replace("git.exe", "sh.exe");
        //        ProcessStartInfo startInfo = new ProcessStartInfo(GitBashPath);
        //        startInfo.Arguments = "--login -i -c \"" + command + " && read -p 'Press <Enter> to continue'\"";
        //        //startInfo.RedirectStandardInput = true;
        //        startInfo.RedirectStandardError = false;
        //        startInfo.RedirectStandardOutput = false;
        //        startInfo.UseShellExecute = true;
        //        startInfo.CreateNoWindow = false;
        //        //startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        //        //startInfo.ErrorDialog = false;
        //        startInfo.WorkingDirectory = WorkingDirectory;

        //        using (Process process = Process.Start(startInfo))
        //        {
        //            process.WaitForExit();
        //        }

        //        WritePrompt();
        //    }
        //}

        void Done()
        {
            if (flag++ < 1)
            {
                Action act = () =>
                {
                    WritePrompt();
                };
                this.Dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
            }
        }

        void WriteError(string data)
        {
            Action act = () =>
            {
                WriteText(data, new SolidColorBrush(Colors.Crimson));
            };
            this.Dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
        }

        void WriteOutput(string data)
        {
            Action act = () =>
            {
                WriteText(data, isGit ?
                    new SolidColorBrush(Colors.Navy) :
                    new SolidColorBrush(Colors.Black));
            };
            this.Dispatcher.BeginInvoke(act, DispatcherPriority.ApplicationIdle);
        }

        void ChangePrompt(string command, Brush brush)
        {
            this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
            var range = this.richTextBox1.Selection;
            range.Select(
                richTextBox1.CaretPosition.GetLineStartPosition(0).GetPositionAtOffset(prompt.Length + 1, LogicalDirection.Forward),
                richTextBox1.CaretPosition.GetLineStartPosition(1) ?? this.richTextBox1.CaretPosition.DocumentEnd);
            range.Text = command;
            range.ApplyPropertyValue(ForegroundProperty, brush);
            this.richTextBox1.ScrollToEnd();
            this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
        }

        void ReplacePrompt(object sender, EventArgs e)
        {
            var newprompt = string.Format("[{1}]>", workingDirectory,
                GitIntellisenseHelper.GetPrompt(tracker));

            if (prompt != newprompt)
            {
                var command = GetCommand();
                var last = this.richTextBox1.Document.Blocks.Last();
                this.richTextBox1.Document.Blocks.Remove(last);
                prompt = newprompt;
                WritePrompt();
                ChangePrompt(command, new SolidColorBrush(Colors.Black));
                lstOptions.Visibility = Visibility.Collapsed;
            }
        }

        private void WritePrompt()
        {
            Paragraph para = new Paragraph();
            para.Margin = new Thickness(0);
            para.FontFamily = new FontFamily("Lucida Console");
            para.LineHeight = 10;
            para.Inlines.Add(new Run(prompt));
            this.richTextBox1.Document.Blocks.Add(para);

            this.richTextBox1.ScrollToEnd();
            this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
        }

        private void WriteText(string text, Brush brush)
        {
            Paragraph para = new Paragraph();
            para.Margin = new Thickness(0);
            para.FontFamily = new FontFamily("Lucida Console");
            para.LineHeight = 10;
            para.Inlines.Add(new Run(text) { Foreground = brush });
            this.richTextBox1.Document.Blocks.Add(para);

            this.richTextBox1.ScrollToEnd();
            this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
        }

        private bool ProcessInternalCommand(string command)
        {
            command = command.ToLower();
            if (string.IsNullOrWhiteSpace(command))
            {
                WritePrompt();
                return true;
            }
            else if (command == "help")
            {
                WriteHelp();
                WritePrompt();
                return true;
            }
            else if (command == "clear" || command == "cls")
            {
                this.richTextBox1.Document.Blocks.Clear();
                WritePrompt();
                return true;
            }
            return false;
        }

        internal void Run(string command)
        {
            ChangePrompt(command, new SolidColorBrush(Colors.Green));
            RunCommand(command);
        }

        #endregion

        #region intellisense

        private void ShowOptions(string command)
        {          
            var options = GetOptions(command);
            if (options != null && options.Any())
            {
                Rect rect = this.richTextBox1.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
                double d = this.ActualHeight - (rect.Y + lstOptions.Height + 12);
                double left = rect.X + 6;
                double top = d > 0 ? rect.Y + 12 : rect.Y - lstOptions.Height; 
                left += this.Padding.Left;
                top += this.Padding.Top;
                lstOptions.SetCurrentValue(ListBox.MarginProperty, new Thickness(left, top, 0, 0));
                lstOptions.ItemsSource = options;
                lstOptions.Visibility = Visibility.Visible;
            }
        }

        private void lstOptions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            InsertText(lstOptions.SelectedValue as string);
        }

        private void lstOptions_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Tab || e.Key == Key.Space)
            {
                InsertText(lstOptions.SelectedValue as string);
                e.Handled = true;
            }
            else if (e.Key == Key.Back || e.Key == Key.Escape)
            {
                this.richTextBox1.Focus();
                this.lstOptions.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void InsertText(string text)
        {
            this.richTextBox1.Focus();
            this.richTextBox1.CaretPosition.InsertTextInRun(text);
            this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
            this.lstOptions.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region git command intellisense
        private IEnumerable<string> GetOptions(string command)
        {
            return GitIntellisenseHelper.GetOptions(tracker, command);
        }
        #endregion

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.richTextBox1.Focus();
        }
    }
}
