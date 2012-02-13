using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

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
                    prompt = workingDirectory + ">";
                    this.richTextBox1.Document.Blocks.Clear();
                    WritePrompt(); 
                }
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void richTextBox1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
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
                ChangePrompt("");
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
                if (text.Contains(">")) e.Handled = true;
            }
        }

        private void GetCommand(int idx)
        {
            if (commandHistory.Count > 0)
            {
                if (idx < 0) idx = 0;
                else if (idx > commandHistory.Count - 1) idx = commandHistory.Count - 1;
                var command = commandHistory[idx];
                commandIdx = idx;
                ChangePrompt(command);
            }
        }

        private void ChangePrompt(string command)
        {
            this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
            var range = this.richTextBox1.Selection;
            range.Select(
                richTextBox1.CaretPosition.GetLineStartPosition(0).GetPositionAtOffset(prompt.Length + 1, LogicalDirection.Forward),
                richTextBox1.CaretPosition.GetLineStartPosition(1) ?? this.richTextBox1.CaretPosition.DocumentEnd);
            range.Text = command;
            this.richTextBox1.ScrollToEnd();
            this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
        }
        
        private void richTextBox1_GotFocus(object sender, RoutedEventArgs e)
        {
            this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
        }

        private void RunCommand(string command)
        {
            var isGit = true;
            command = command.Substring(command.IndexOf(">") + 1).Trim();

            if (!string.IsNullOrWhiteSpace(command) && 
               (commandHistory.Count==0 || commandHistory.Last()!=command))
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
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();

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
                    WriteText(error, new SolidColorBrush(Colors.DarkRed));
                }
                WritePrompt();
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
    }
}
