using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;

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

            cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdPendingPullRebase);
            menu = new MenuCommand(OnPullRebaseCommand, cmd);
            mcs.AddCommand(menu);
            
            cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdPendingPush);
            menu = new MenuCommand(OnPushCommand, cmd);
            mcs.AddCommand(menu);

            cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdPendingChangesAmend);
            menu = new MenuCommand(new EventHandler(OnAmendCommitCommand), cmd);
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

        private void OnPullRebaseCommand(object sender, EventArgs e)
        {
            ((PendingChangesView)control).PullRebase();
        }
        
        private void OnPushCommand(object sender, EventArgs e)
        {
            ((PendingChangesView)control).Push();
        }

        private void OnAmendCommitCommand(object sender, EventArgs e)
        {
            ((PendingChangesView) control).AmendCommit();
        }

        internal void Refresh(GitFileStatusTracker tracker)
        {
            //var frame = this.Frame as IVsWindowFrame;
            //if (frame == null || frame.IsVisible() == 1) return;

            try
            {
                this.Caption = Resources.ResourceManager.GetString("PendingChangesToolWindowCaption");

                ((PendingChangesView)control).Refresh(tracker);

                var repository = (tracker == null || !tracker.HasGitRepository) ? "" :
                    string.Format(" - {1} - ({0})", tracker.CurrentBranch, tracker.GitWorkingDirectory);

                this.Caption = Resources.ResourceManager.GetString("PendingChangesToolWindowCaption") + repository;
            }
            catch (Exception ex)
            {
                Log.WriteLine("Pending Changes Tool Window Refresh: {0}", ex.ToString());
            }
        }
    }
}
