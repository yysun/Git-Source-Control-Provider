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
using Microsoft.VisualStudio;
using System.Collections.Generic;

namespace GitScc
{
    /// <summary>
    /// Summary description for SccProviderToolWindow.
    /// </summary>
    [Guid("75EDECF4-68D8-4B7B-92A9-5915461DA6D9")]
    public class PendingChangesToolWindow : ToolWindowWithEditor
    {
        //private PendingChangesView control;

        public PendingChangesToolWindow()
        {
            // set the window title
            this.Caption = Resources.ResourceManager.GetString("PendingChangesToolWindowCaption");

            //// set the CommandID for the window ToolBar
            base.ToolBar = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.imnuPendingChangesToolWindowToolbarMenu);

            // set the icon for the frame
            this.BitmapResourceID = CommandId.ibmpToolWindowsImages;  // bitmap strip resource ID
            this.BitmapIndex = CommandId.iconSccProviderToolWindow;   // index in the bitmap strip
        }

        protected override void Initialize()
        {
            base.Initialize();
            control = new PendingChangesView(this);

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on 
            // the object returned by the Content property.
            base.Content = control;

            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            var cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdPendingChangesCommit);
            var menu = new MenuCommand(new EventHandler(OnCommitCommand), cmd);
            mcs.AddCommand(menu);

            cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdPendingChangesAmend);
            menu = new MenuCommand(new EventHandler(OnAmendCommitCommand), cmd);
            mcs.AddCommand(menu);

            cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdPendingChangesCommitToBranch);
            menu = new MenuCommand(new EventHandler(OnCommitToBranchCommand), cmd);
            mcs.AddCommand(menu);
            
            var sccProviderService = BasicSccProvider.GetServiceEx<SccProviderService>();
            if (sccProviderService != null)
            {
                Refresh(sccProviderService.CurrentTracker);
            }
        }

        private void OnCommitCommand(object sender, EventArgs e)
        {
            ((PendingChangesView) control).Commit();
        }

        private void OnAmendCommitCommand(object sender, EventArgs e)
        {
            ((PendingChangesView) control).AmendCommit();
        }

        private void OnCommitToBranchCommand(object sender, EventArgs e)
        {
            ((PendingChangesView)control).CommitToBranch();
        }

        internal void Refresh(GitFileStatusTracker tracker)
        {
            //if (((IVsWindowFrame)this.Frame).IsVisible() == VSConstants.S_FALSE) return;

            ((PendingChangesView) control).Refresh(tracker);

            var repository = (tracker == null || !tracker.HasGitRepository) ? " (no repository)" :
                string.Format(" - {1} - ({0})", tracker.CurrentBranch, tracker.GitWorkingDirectory);

            this.Caption = Resources.ResourceManager.GetString("PendingChangesToolWindowCaption") + repository;
                
        }
    }
}
