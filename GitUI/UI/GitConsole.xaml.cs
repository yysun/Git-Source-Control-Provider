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
                var rtb = this.richTextBox1;
                var command = new TextRange(rtb.CaretPosition.GetLineStartPosition(0),
                    rtb.CaretPosition.GetLineStartPosition(1) ?? rtb.CaretPosition.DocumentEnd).Text;
                command = command.Replace("\r", "").Replace("\n", "");
                RunCommand(command);
            }
        }
        
        private void richTextBox1_GotFocus(object sender, RoutedEventArgs e)
        {
            this.richTextBox1.CaretPosition = this.richTextBox1.CaretPosition.DocumentEnd;
        }

        private void RunCommand(string command)
        {
            var isGit = true;
            command = command.Substring(command.IndexOf(">") + 1).Trim();
            if (!ProcessInternalCommand(command))
            {
                if (command.StartsWith("git "))
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
            var prompt =  this.WorkingDirectory + ">";
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
