using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;

namespace GitScc
{

    [Guid("9175DE5D-630E-4E7B-A352-CFFD6132553C")]
    public class HistoryToolWindow : ToolWindowWithEditor
    {
        public HistoryToolWindow() : base()
        {
            // set the window title
            this.Caption = Resources.ResourceManager.GetString("HistoryToolWindowCaption");

            // set the CommandID for the window ToolBar
            this.ToolBar = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.imnuHistoryToolWindowToolbarMenu);
                                                                                    
            // set the icon for the frame
            this.BitmapResourceID = CommandId.ibmpToolWindowsImages;  // bitmap strip resource ID
            this.BitmapIndex = CommandId.iconSccProviderToolWindow;   // index in the bitmap strip
        }

        protected override void Initialize()
        {
            base.Initialize();
            control = new HistoryView(this);

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
            ((HistoryView) control).Refresh(tracker);

            var repository = (tracker == null || !tracker.HasGitRepository) ? "" :
                string.Format(" - {1} - ({0})", tracker.CurrentBranch, tracker.GitWorkingDirectory);

            this.Caption = Resources.ResourceManager.GetString("HistoryToolWindowCaption") + repository;
        }

    }
}
