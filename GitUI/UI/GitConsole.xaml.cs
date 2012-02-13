using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GitScc.DataServices;

namespace GitUI.UI
{
    /// <summary>
    /// Interaction logic for GitConsole.xaml
    /// </summary>
    public partial class GitConsole : UserControl
    {
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
                    //prompt = string.Format("{0} ({1})\r\n>", workingDirectory,
                    //    GitViewModel.Current.Tracker.CurrentBranch);

                    prompt = string.Format("({1})>", workingDirectory,
                        GitViewModel.Current.Tracker.CurrentBranch);

                    this.richTextBox1.Document.Blocks.Clear();
                    WritePrompt(); 
                }
            }
        }

        #region keydown event
        private void richTextBox1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                var command = new TextRange(richTextBox1.CaretPosition.GetLineStartPosition(0),
                   richTextBox1.CaretPosition).Text;
                command = command.Substring(command.IndexOf(">") + 1).Trim();
                ShowOptions(command);
                return;
            }
            else if (e.Key == Key.Tab || e.Key == Key.Down)
            {
                if (lstOptions.Visibility == Visibility.Visible)
                {
                    lstOptions.Focus();
                    return;
                }
            }
            else
            {
                lstOptions.Visibility = Visibility.Collapsed;
            }

            if (e.Key == Key.Enter)
            {
                var command = new TextRange(richTextBox1.CaretPosition.GetLineStartPosition(0),
                    richTextBox1.CaretPosition.GetLineStartPosition(1) ?? richTextBox1.CaretPosition.DocumentEnd).Text;
                command = command.Replace("\r", "").Replace("\n", "");
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
                ChangePrompt("",  new SolidColorBrush(Colors.Black));
            }
            else if (e.Key == Key.Back)
            {
                var text = new TextRange(richTextBox1.CaretPosition.GetLineStartPosition(0),
                    richTextBox1.CaretPosition).Text;
                if (text.EndsWith(">")) e.Handled = true;
            }
            else
            {
                var text = new TextRange(richTextBox1.CaretPosition, richTextBox1.CaretPosition.DocumentEnd).Text;
                if (text.Contains(">")) //e.Handled = true;
                    this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
            }
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
                ChangePrompt(command,  new SolidColorBrush(Colors.Black));
            }
        }

        private void RunCommand(string command)
        {
            var isGit = true;
            command = command.Substring(command.IndexOf(">") + 1).Trim();

            if (!string.IsNullOrWhiteSpace(command) &&
               (commandHistory.Count == 0 || commandHistory.Last() != command))
            {
                commandHistory.Add(command);
                commandIdx = commandHistory.Count;
            }

            if (!ProcessInternalCommand(command))
            {
                if (command == "git")
                {
                    command = "/C \"\"" + GitExePath + "\"";
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
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.WorkingDirectory = WorkingDirectory;
                Process p = Process.Start(startInfo);
                output = p.StandardOutput.ReadToEnd();
                error = p.StandardError.ReadToEnd();

                p.WaitForExit();
                if (output.Length != 0)
                {
                    WriteText(output, isGit ?
                        new SolidColorBrush(Colors.Navy) :
                        new SolidColorBrush(Colors.Black)
                    );
                }
                else if (error.Length != 0)
                {
                    WriteText(error, new SolidColorBrush(Colors.Crimson));
                }
                WritePrompt();
            }
        }

        private void ChangePrompt(string command, Brush brush)
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
            para.FontFamily = new FontFamily("Lucida Console");
            para.Inlines.Add(new Run(text) { Foreground = brush });
            this.richTextBox1.Document.Blocks.Add(para);

            this.richTextBox1.ScrollToEnd();
            this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
        }

        private bool ProcessInternalCommand(string command)
        {
            command = command.ToLower();
            if (command == "clear" || command == "cls")
            {
                this.richTextBox1.Document.Blocks.Clear();
                WritePrompt();
                return true;
            }
            return false;
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
            switch (command)
            {
                case "git":
                    return new string[] { 
                        "add", "bisect", "branch", "checkout", "clone",
                        "commit", "diff", "fetch", "grep", "init",  
                        "log", "merge", "mv", "pull", "push", "rebase",
                        "reset", "rm", "show", "status", "tag"
                    };

                case "git checkout":
                    if (GitViewModel.Current.Tracker.HasGitRepository)
                    {
                        return GitViewModel.Current.Tracker.RepositoryGraph.Refs
                            .Where(r => r.Type == RefTypes.Branch)
                            .Select(r => r.Name);
                    }
                    break;

                case "git branch -D":
                case "git branch -d":
                    if (GitViewModel.Current.Tracker.HasGitRepository)
                    {
                        return GitViewModel.Current.Tracker.RepositoryGraph.Refs
                            .Where(r => r.Type == RefTypes.Branch)
                            .Select(r => r.Name);
                    }
                    break;

                case "git tag -d":
                    if (GitViewModel.Current.Tracker.HasGitRepository)
                    {
                        return GitViewModel.Current.Tracker.RepositoryGraph.Refs
                            .Where(r => r.Type == RefTypes.Tag)
                            .Select(r => r.Name);
                    }
                    break;
            }
            return new string[] { };
        }
        #endregion

        string output, error;

        internal string Run(string command)
        {
            ChangePrompt(command, new SolidColorBrush(Colors.Green));
            RunCommand(command);

            return output + error;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            GitViewModel.Current.console = this;
        }
    }
}
