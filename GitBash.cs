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
        private static string gitExePath;
        public static string GitExePath
        {
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

        public static string Run(string args, string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(gitExePath) || !File.Exists(gitExePath))
                throw new Exception("Git Executable not found");

            Debug.WriteLine(string.Format("{2}>{0} {1}", gitExePath, args, workingDirectory));

            var pinfo = new ProcessStartInfo(gitExePath)
            {
                Arguments = args,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };

            using (var process = Process.Start(pinfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Debug.WriteLine(output);

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.WriteLine("STDERR: " + error);
                    throw new Exception(error);
                }
                return output;
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

            using (var process = Process.Start(pinfo))
            {
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error))
                    throw new Exception(error);
            }
        }
    }
}
