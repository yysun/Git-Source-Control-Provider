using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Configuration;
using System.IO;

namespace GitScc
{
    public abstract class GitBash
    {
        public static bool UseUTF8FileNames { get; set; }

        private static string gitExePath;
        public static string GitExePath
        {
            get { return gitExePath; }
            set
            {
                try
                {
                    gitExePath = value == null ? null : Path.Combine(Path.GetDirectoryName(value), "git.exe");
                }
                catch{}
            }
        }

        public static bool Exists { get { return !string.IsNullOrWhiteSpace(gitExePath) &&
            File.Exists(gitExePath); } }

        public static GitBashResult Run(string args, string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(gitExePath) || !File.Exists(gitExePath))
                throw new Exception("Git Executable not found");

            GitBashResult result = new GitBashResult();

            //Debug.WriteLine(string.Format("{2}>{0} {1}", gitExePath, args, workingDirectory));

            var pinfo = new ProcessStartInfo(gitExePath)
            {
                Arguments = args,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };

            if (UseUTF8FileNames)
            {
                pinfo.StandardOutputEncoding = Encoding.UTF8;
                pinfo.StandardErrorEncoding = Encoding.UTF8;
            }

            using (var process = Process.Start(pinfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                //Debug.WriteLine(output);

                result.HasError = process.ExitCode != 0;
                result.Output = output;
                result.Error = error;

                return result;
            }
        }

        public static void RunCmd(string args, string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(gitExePath) || !File.Exists(gitExePath))
                throw new Exception("Git Executable not found");

            Debug.WriteLine(string.Format("{2}>{0} {1}", gitExePath, args, workingDirectory));

            var pinfo = new ProcessStartInfo("cmd.exe")
            {
                Arguments = "/C \"\"" + gitExePath + "\" " + args + "\"",
                CreateNoWindow = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };
            if (UseUTF8FileNames)
            {
                pinfo.StandardErrorEncoding = Encoding.UTF8;
            }
            using (var process = Process.Start(pinfo))
            {
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error))
                    throw new Exception(error);
            }
        }

        public static void OpenGitBash(string workingDirectory)
        {
            if (!Exists) return;

            var gitBashPath = gitExePath.Replace("git.exe", "sh.exe");
            RunDetatched("cmd.exe", string.Format("/c \"{0}\" --login -i", gitBashPath), workingDirectory);
        }

        internal static void RunDetatched(string cmd, string arguments, string workingDirectory)
        {
            using (Process process = new Process())
            {
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.ErrorDialog = false;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.RedirectStandardInput = false;

                process.StartInfo.CreateNoWindow = false;
                process.StartInfo.FileName = cmd;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.WorkingDirectory = workingDirectory;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                process.StartInfo.LoadUserProfile = true;

                process.Start();
            }
        }
    }
}
