using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GitScc
{
    /// <summary>
    /// Interaction logic for HistoryView.xaml
    /// </summary>
    public partial class HistoryView : UserControl
    {
        HistoryToolWindow toolWindow;
        private GitFileStatusTracker tracker;

        public HistoryView(HistoryToolWindow toolWindow)
        {
            InitializeComponent();
            this.toolWindow = toolWindow;
        }

        public void InsertNewEditor(object editor)
        {
            diffEditorHost.Content = editor;
        }

        internal void Refresh(GitFileStatusTracker tracker)
        {
            this.tracker = tracker;
            if (tracker == null)
            {
                //this.dataGrid1.ItemsSource = null;
                return;
            }
        }

        private void OpenFile(string fileName)
        {
            this.toolWindow.SetDisplayedFile(fileName);
        }        
    }
}
