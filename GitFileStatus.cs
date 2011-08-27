using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;

namespace GitScc
{
    public enum GitFileStatus
    {
        NotControlled,
        New,
        Tracked,
        Modified,
        Staged,
        Removed,
        Added,
        Deleted,
        MergeConflict,
        Ignored,
    }

    public class GitFile : INotifyPropertyChanged
    {
        public GitFileStatus Status { get; set; }
        public string FileName { get; set; }
        public bool IsStaged { get; set; }

        public bool isSelected;
        public bool IsSelected 
        { 
            get { return isSelected; }
            set { isSelected = value; OnPropertyChanged("IsSelected"); } 
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

    }
}
