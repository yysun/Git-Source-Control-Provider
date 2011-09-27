using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NGit;
using NGit.Treewalk;
using System.ComponentModel;

namespace GitScc.DataServices
{
    public class GitTreeObject : INotifyPropertyChanged
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public Repository repository { get; internal set; }
        public bool IsTree { get; set; }

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

                    var treeId = ObjectId.FromString(this.Id);

                    var tree = new Tree(repository, treeId, repository.Open(treeId).GetBytes());
                    children = (from t in tree.Members()
                                select new GitTreeObject
                                {
                                    Id = t.GetId().Name,
                                    Name = t.GetName(),
                                    repository = this.repository,
                                    IsTree = t.GetMode().GetObjectType() == Constants.OBJ_TREE
                                }).ToList();
                }
                return children;
            }
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
                if (!IsTree && content == null)
                {
                    try
                    {
                        var blob = this.repository.Open(ObjectId.FromString(this.Id));
                        if (blob != null) content = blob.GetCachedBytes();
                    }
                    catch { } //better than crash
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