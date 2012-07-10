using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;

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

        private static GitSccOptions gitSccOptions;

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

            if (string.IsNullOrEmpty(DifftoolPath)) DifftoolPath = "diffmerge.exe";
        }

        public void SaveConfig()
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
