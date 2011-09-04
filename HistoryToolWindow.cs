using System;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using IServiceProvider = System.IServiceProvider;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace GitScc
{

    [Guid("9175DE5D-630E-4E7B-A352-CFFD6132553C")]
    public class HistoryToolWindow : ToolWindowPane
    {
        private HistoryView control;

        public HistoryToolWindow()
            : base(null)
        {
            // set the window title
            this.Caption = Resources.ResourceManager.GetString("HistoryToolWindowCaption");

            // set the CommandID for the window ToolBar
            this.ToolBar = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.imnuHistoryToolWindowToolbarMenu);
                                                                                    
            // set the icon for the frame
            this.BitmapResourceID = CommandId.ibmpToolWindowsImages;  // bitmap strip resource ID
            this.BitmapIndex = CommandId.iconSccProviderToolWindow;   // index in the bitmap strip

            control = new HistoryView();

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on 
            // the object returned by the Content property.
            base.Content = control;

            var sccProviderService = BasicSccProvider.GetServiceEx<SccProviderService>();
            if (sccProviderService != null)
            {
                Refresh(sccProviderService.CurrentTracker);
            }

        }

        internal void Refresh(GitFileStatusTracker tracker)
        {

            control.Refresh(tracker);

            var repository = (tracker == null || !tracker.HasGitRepository) ? " (no repository)" :
                string.Format(" {1} - ({0})", tracker.CurrentBranch, tracker.GitWorkingDirectory);

            this.Caption = Resources.ResourceManager.GetString("HistoryToolWindowCaption") + repository;

        }
    }
}
