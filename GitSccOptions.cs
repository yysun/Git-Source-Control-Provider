﻿using System;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace GitScc
{
    [Serializable]
    public class GitSccOptions
    {
        private static string configFileName = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "gitscc.config");

        public string GitBashPath       { get; set; }
        public string GitExtensionPath  { get; set; }
        public string DifftoolPath      { get; set; }
        public string TortoiseGitPath   { get; set; }
        public bool NotExpandTortoiseGit { get; set; }
        public bool NotExpandGitExtensions { get; set; }
        public bool UseTGitIconSet { get; set; }
        public bool DisableAutoRefresh { get; set; }
        public bool DisableAutoLoad { get; set; }
        public bool NotUseUTF8FileNames { get; set; }
        public bool DisableDiffMargin { get; set; }
        public bool UseVsDiff { get; set; }

        private static GitSccOptions gitSccOptions;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static GitSccOptions Current
        {
            get
            {
                if (gitSccOptions == null)
                {
                    gitSccOptions = LoadFromConfig();
                }
                return gitSccOptions;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static bool IsVisualStudio2010
        {
            get
            {
                return !IsVisualStudio2012 && BasicSccProvider.GetGlobalService(typeof(SVsSolution)) is IVsSolution4;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static bool IsVisualStudio2012
        {
            get
            {
                return BasicSccProvider.GetGlobalService(typeof(SVsDifferenceService)) != null;
            }
        }

        private GitSccOptions()
        {
            
        }

        internal static GitSccOptions LoadFromConfig()
        {
            GitSccOptions options = null;

            
            if (File.Exists(configFileName))
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(GitSccOptions));
                    using (TextReader tr = new StreamReader(configFileName))
                    {
                        options = (GitSccOptions)serializer.Deserialize(tr);
                    }
                }
                catch
                {
                }
            }

            if(options == null) options = new GitSccOptions();

            options.Init();

            return options;
        }

        private void Init()
        {
            if (string.IsNullOrEmpty(GitBashPath))
            {
                GitBashPath = TryFindFile(new string[]{
                    @"C:\Program Files\Git\bin\sh.exe",
                    @"C:\Program Files (x86)\Git\bin\sh.exe",
                });
            }
            if (string.IsNullOrEmpty(GitExtensionPath))
            {
                GitExtensionPath = TryFindFile(new string[]{
                    @"C:\Program Files\GitExtensions\GitExtensions.exe",
                    @"C:\Program Files (x86)\GitExtensions\GitExtensions.exe",
                });
            }
            if (string.IsNullOrEmpty(TortoiseGitPath))
            {
                TortoiseGitPath = TryFindFile(new string[]{
                    @"C:\Program Files\TortoiseGit\bin\TortoiseProc.exe",
                    @"C:\Program Files (x86)\TortoiseGit\bin\TortoiseProc.exe",
                });
            }
            if (string.IsNullOrEmpty(TortoiseGitPath))
            {
                TortoiseGitPath = TryFindFile(new string[]{
                    @"C:\Program Files\TortoiseGit\bin\TortoiseGitProc.exe",
                    @"C:\Program Files (x86)\TortoiseGit\bin\TortoiseGitProc.exe",
                });
            }
            if (string.IsNullOrEmpty(DifftoolPath)) DifftoolPath = "diffmerge.exe";

            bool diffServiceAvailable = Package.GetGlobalService(typeof(SVsDifferenceService)) != null;
            if (!diffServiceAvailable)
                UseVsDiff = false;
        }

        internal void SaveConfig()
        {
            try
            {
                XmlSerializer x = new XmlSerializer(typeof(GitSccOptions));
                using (TextWriter tw = new StreamWriter(configFileName))
                {
                    x.Serialize(tw, this);
                }
            }
            catch { }
        }

        private string TryFindFile(string[] paths)
        {
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }
    }
}
