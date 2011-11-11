using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;

namespace GitScc
{
    [Guid("9175DE5D-630E-4E7B-A352-CFFD6132553C")]
    public class HistoryToolWindow : ToolWindowWithEditor
    {
        private SccProviderService sccProviderService;

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

            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            var cmd = new CommandID(GuidList.guidSccProviderCmdSet, CommandId.icmdHistoryViewRefresh);
            var menu = new MenuCommand(new EventHandler(OnRefreshCommand), cmd);
            mcs.AddCommand(menu);

            sccProviderService = BasicSccProvider.GetServiceEx<SccProviderService>();
            
        }

        private void OnRefreshCommand(object sender, EventArgs e)
        {
            Refresh(sccProviderService.CurrentTracker, true);
        }

        internal void Refresh(GitFileStatusTracker tracker, bool force = false)
        {
            //var frame = this.Frame as IVsWindowFrame;
            //if (frame == null || frame.IsVisible() == 1) return;

            try
            {
                var repository = (tracker == null || !tracker.HasGitRepository) ? "" :
                    string.Format(" - {0}", tracker.CurrentBranch, tracker.GitWorkingDirectory);

                this.Caption = Resources.ResourceManager.GetString("HistoryToolWindowCaption") + repository;

                if (!GitSccOptions.Current.DisableAutoRefresh || force || tracker == null)
                {
                    ((HistoryView)control).Refresh(tracker);
                }
                else
                {
                    this.Caption += " - [AUTO REFRESH DISABLED]";
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine("History Tool Window Refresh: {0}", ex.ToString());
            }

        }

    }
}
