using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel;
using System.IO;

namespace GitScc.DataServices
{
    public class GitTreeObject : INotifyPropertyChanged
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string FullName { get; internal set; }
        public string Type { get; set; }
        public string Mode { get; set; }
        public string Repository { get; internal set; }

        public bool IsBlob
        {
            get { return Type == "blob"; }
        }

        public bool IsCommit
        {
            get { return Type == "commit"; }
        }

        public bool IsTree
        {
            get { return Type == "tree"; }
        }

        private bool isExpanded;
        private IEnumerable<GitTreeObject> children;
        private byte[] content;

        public IEnumerable<GitTreeObject> Children
        {
            get
            {
                if (children == null)
                {
                    if (!IsTree) return null;
                    if (!IsExpanded) return new List<GitTreeObject> { new GitTreeObject() }; // a place holder

                    try
                    {
                        var result = GitBash.Run("ls-tree -z \"" + Id + "\"", this.Repository);
                        if (!result.HasError)
                            children = result.Output.Split(new char[] { '\0', '\n' })
                                       .Select(t => ParseString(t))
                                       .OfType<GitTreeObject>();
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine("GitTreeObject.Children: {0} - {1}\r\n{2}", Id, this.Repository, ex.ToString());
                    }

                }
                return children;
            }
        }

        private GitTreeObject ParseString(string itemsString)
        {
            if (string.IsNullOrWhiteSpace(itemsString) || (itemsString.Length <= 53))
                return null;

            var guidStart = itemsString.IndexOf(' ', 7);
            var name = itemsString.Substring(guidStart + 42).Trim();
            var fullName = this.FullName.Length == 0 ? name : this.FullName + "/" + name;

            return new GitTreeObject
            {
                Mode = itemsString.Substring(0, 6),
                Type = itemsString.Substring(7, guidStart - 7).ToLower(),
                Id = itemsString.Substring(guidStart + 1, 40),
                Name = name,
                FullName = fullName,
                Repository = this.Repository,
            };
        }

        public bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                isExpanded = value;
                NotifyPropertyChanged("Children");
            }
        }

        public byte[] Content
        {
            get
            {
                if (IsBlob && content == null)
                {
                    try
                    {
                        var fileName = Path.GetTempFileName();

                        GitBash.RunCmd(string.Format("cat-file blob {0} > {1}", this.Id, fileName), this.Repository);

                        content = File.ReadAllBytes(fileName);

                        if (File.Exists(fileName)) File.Delete(fileName);
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine("GitTreeObject.Content: {0} - {1}\r\n{2}", Id, this.Repository, ex.ToString());
                    }
                }
                return content;
            }
        }

        #region INotifyPropertyChanged
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        #endregion
    }
}