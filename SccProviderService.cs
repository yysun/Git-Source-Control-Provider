using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace GitScc
{
    [Guid("C4128D99-1000-41D1-A6C3-704E6C1A3DE2")]
    public class SccProviderService : IVsSccProvider,
        IVsSccManager2,
        IVsSccManagerTooltip,
        IVsSolutionEvents,
        IVsSolutionEvents2,
        IVsSccGlyphs,
        IDisposable
    {
        private bool _active = false;
        private BasicSccProvider _sccProvider = null;
        private uint _vsSolutionEventsCookie;

        private GitFileStatusTracker _statusTracker = null;

        #region SccProvider Service initialization/unitialization
        public SccProviderService(BasicSccProvider sccProvider)
        {
            _sccProvider = sccProvider;
            _statusTracker = new GitFileStatusTracker();
            //_statusTracker.OnGitRepoChanged += new EventHandler(_statusTracker_OnGitRepoChanged);

            // Subscribe to solution events
            IVsSolution sol = (IVsSolution)_sccProvider.GetService(typeof(SVsSolution));
            sol.AdviseSolutionEvents(this, out _vsSolutionEventsCookie);
            Debug.Assert(VSConstants.VSCOOKIE_NIL != _vsSolutionEventsCookie);

        }

        public void Dispose()
        {
            //_statusTracker.OnGitRepoChanged -= new EventHandler(_statusTracker_OnGitRepoChanged);          

            // Unregister from receiving solution events
            if (VSConstants.VSCOOKIE_NIL != _vsSolutionEventsCookie)
            {
                IVsSolution sol = (IVsSolution)_sccProvider.GetService(typeof(SVsSolution));
                sol.UnadviseSolutionEvents(_vsSolutionEventsCookie);
                _vsSolutionEventsCookie = VSConstants.VSCOOKIE_NIL;
            }
        }
        #endregion

        #region IVsSccProvider interface functions
        /// <summary>
        /// Returns whether this source control provider is the active scc provider.
        /// </summary>
        public bool Active
        {
            get { return _active; }
        }

        // Called by the scc manager when the provider is activated. 
        // Make visible and enable if necessary scc related menu commands
        public int SetActive()
        {
            Trace.WriteLine(String.Format(CultureInfo.CurrentUICulture, "Provider set active"));
            _active = true;
            _sccProvider.OnActiveStateChange();

            OpenTracker();
            return VSConstants.S_OK;
        }

        // Called by the scc manager when the provider is deactivated. 
        // Hides and disable scc related menu commands
        public int SetInactive()
        {
            Trace.WriteLine(String.Format(CultureInfo.CurrentUICulture, "Provider set inactive"));

            _active = false;
            _sccProvider.OnActiveStateChange();

            return VSConstants.S_OK;
        }

        public int AnyItemsUnderSourceControl(out int pfResult)
        {
            pfResult = 0;
            return VSConstants.S_OK;
        }
        #endregion

        #region IVsSccManager2 interface functions

        public int BrowseForProject(out string pbstrDirectory, out int pfOK)
        {
            // Obsolete method
            pbstrDirectory = null;
            pfOK = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int CancelAfterBrowseForProject()
        {
            // Obsolete method
            return VSConstants.E_NOTIMPL;
        }

        /// <summary>
        /// Returns whether the source control provider is fully installed
        /// </summary>
        public int IsInstalled(out int pbInstalled)
        {
            // All source control packages should always return S_OK and set pbInstalled to nonzero
            pbInstalled = 1;
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Provide source control icons for the specified files and returns scc status of files
        /// </summary>
        /// <returns>The method returns S_OK if at least one of the files is controlled, S_FALSE if none of them are</returns>
        public int GetSccGlyph([InAttribute] int cFiles, [InAttribute] string[] rgpszFullPaths, [OutAttribute] VsStateIcon[] rgsiGlyphs, [OutAttribute] uint[] rgdwSccStatus)
        {
            Debug.Assert(cFiles == 1, "Only getting one file icon at a time is supported");
            // Return the icons and the status. While the status is a combination a flags, we'll return just values 
            // with one bit set, to make life easier for GetSccGlyphsFromStatus
            GitFileStatus status = _statusTracker.GetFileStatus(rgpszFullPaths[0]);

            switch (status)
            {
                case GitFileStatus.Trackered:
                    rgsiGlyphs[0] = VsStateIcon.STATEICON_CHECKEDIN;
                    if (rgdwSccStatus != null)
                    {
                        rgdwSccStatus[0] = (uint)__SccStatus.SCC_STATUS_CONTROLLED;
                    }
                    break;

                case GitFileStatus.Modified:
                    rgsiGlyphs[0] = VsStateIcon.STATEICON_CHECKEDOUT;
                    if (rgdwSccStatus != null)
                    {
                        rgdwSccStatus[0] = (uint)__SccStatus.SCC_STATUS_CHECKEDOUT;
                    }
                    break;

                case GitFileStatus.UnTrackered:
                    //rgsiGlyphs[0] = VsStateIcon.STATEICON_CHECKEDOUT;
                    rgsiGlyphs[0] = (VsStateIcon)(this._customSccGlyphBaseIndex + (uint)CustomSccGlyphs.PendingAdd);
                    if (rgdwSccStatus != null)
                    {
                        rgdwSccStatus[0] = (uint)__SccStatus.SCC_STATUS_CHECKEDOUT;
                    }
                    break;

                case GitFileStatus.Staged:
                    //rgsiGlyphs[0] = VsStateIcon.STATEICON_CHECKEDOUT;
                    rgsiGlyphs[0] = (VsStateIcon)(this._customSccGlyphBaseIndex + (uint)CustomSccGlyphs.Staged);
                    if (rgdwSccStatus != null)
                    {
                        rgdwSccStatus[0] = (uint)__SccStatus.SCC_STATUS_CHECKEDOUT;
                    }
                    break;

                case GitFileStatus.NotControlled:
                    rgsiGlyphs[0] = VsStateIcon.STATEICON_BLANK;
                    if (rgdwSccStatus != null)
                    {
                        rgdwSccStatus[0] = (uint)__SccStatus.SCC_STATUS_NOTCONTROLLED;
                    }
                    break;

            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Determines the corresponding scc status glyph to display, given a combination of scc status flags
        /// </summary>
        public int GetSccGlyphFromStatus([InAttribute] uint dwSccStatus, [OutAttribute] VsStateIcon[] psiGlyph)
        {
            switch (dwSccStatus)
            {
                case (uint)__SccStatus.SCC_STATUS_CHECKEDOUT:
                    psiGlyph[0] = VsStateIcon.STATEICON_CHECKEDOUT;
                    break;
                case (uint)__SccStatus.SCC_STATUS_CONTROLLED:
                    psiGlyph[0] = VsStateIcon.STATEICON_CHECKEDIN;
                    break;
                default:
                    psiGlyph[0] = VsStateIcon.STATEICON_BLANK;
                    break;
            }
            return VSConstants.S_OK;
        }

        /// <summary>
        /// One of the most important methods in a source control provider, is called by projects that are under source control when they are first opened to register project settings
        /// </summary>
        public int RegisterSccProject([InAttribute] IVsSccProject2 pscp2Project, [InAttribute] string pszSccProjectName, [InAttribute] string pszSccAuxPath, [InAttribute] string pszSccLocalPath, [InAttribute] string pszProvider)
        {
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called by projects registered with the source control portion of the environment before they are closed. 
        /// </summary>
        public int UnregisterSccProject([InAttribute] IVsSccProject2 pscp2Project)
        {
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsSccManagerTooltip interface functions

        /// <summary>
        /// Called by solution explorer to provide tooltips for items. Returns a text describing the source control status of the item.
        /// </summary>
        public int GetGlyphTipText([InAttribute] IVsHierarchy phierHierarchy, [InAttribute] uint itemidNode, out string pbstrTooltipText)
        {
            string fileName = GetFileName(phierHierarchy, itemidNode);
            GitFileStatus status = _statusTracker.GetFileStatus(fileName);
            pbstrTooltipText = status.ToString();

            return VSConstants.S_OK;
        }

        #endregion

        #region IVsSolutionEvents interface functions

        public int OnAfterOpenSolution([InAttribute] Object pUnkReserved, [InAttribute] int fNewSolution)
        {
            OpenTracker();
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution([InAttribute] Object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterLoadProject([InAttribute] IVsHierarchy pStubHierarchy, [InAttribute] IVsHierarchy pRealHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterOpenProject([InAttribute] IVsHierarchy pHierarchy, [InAttribute] int fAdded)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseProject([InAttribute] IVsHierarchy pHierarchy, [InAttribute] int fRemoved)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseSolution([InAttribute] Object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeUnloadProject([InAttribute] IVsHierarchy pRealHierarchy, [InAttribute] IVsHierarchy pStubHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseProject([InAttribute] IVsHierarchy pHierarchy, [InAttribute] int fRemoving, [InAttribute] ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution([InAttribute] Object pUnkReserved, [InAttribute] ref int pfCancel)
        {
            CloseTracker();
            return VSConstants.S_OK;
        }

        public int OnQueryUnloadProject([InAttribute] IVsHierarchy pRealHierarchy, [InAttribute] ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterMergeSolution([InAttribute] Object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsSccGlyphs Members

        // Remember the base index where our custom scc glyph start
        private uint _customSccGlyphBaseIndex = 0;
        // Our custom image list
        ImageList _customSccGlyphsImageList;
        // Indexes of icons in our custom image list
        enum CustomSccGlyphs
        {
            PendingAdd = 0,
            Staged = 1,
        };

        public int GetCustomGlyphList(uint BaseIndex, out uint pdwImageListHandle)
        {
            // If this is the first time we got called, construct the image list, remember the index, etc
            if (this._customSccGlyphsImageList == null)
            {
                // The shell calls this function when the provider becomes active to get our custom glyphs
                // and to tell us what's the first index we can use for our glyphs
                // Remember the index in the scc glyphs (VsStateIcon) where our custom glyphs will start
                this._customSccGlyphBaseIndex = BaseIndex;

                // Create a new imagelist
                this._customSccGlyphsImageList = new ImageList();

                // Set the transparent color for the imagelist (the SccGlyphs.bmp uses magenta for background)
                this._customSccGlyphsImageList.TransparentColor = Color.FromArgb(255, 0, 255);

                // Set the corret imagelist size (7x16 pixels, otherwise the system will either stretch the image or fill in with black blocks)
                this._customSccGlyphsImageList.ImageSize = new Size(7, 16);

                // Add the custom scc glyphs we support to the list
                // NOTE: VS2005 and VS2008 are limited to 4 custom scc glyphs (let's hope this will change in future versions)
                Image sccGlyphs = (Image)Resources.SccGlyphs;
                this._customSccGlyphsImageList.Images.AddStrip(sccGlyphs);
            }

            // Return a Win32 HIMAGELIST handle to our imagelist to the shell (by keeping the ImageList a member of the class we guarantee the Win32 object is still valid when the shell needs it)
            pdwImageListHandle = (uint)this._customSccGlyphsImageList.Handle;

            // Return success (If you don't want to have custom glyphs return VSConstants.E_NOTIMPL)
            return VSConstants.S_OK;

        }

        #endregion

        private string solutionDirectoryName;

        private string GetFileName(IVsHierarchy hierHierarchy, uint itemidNode)
        {
            string pvalue;
            return hierHierarchy.GetCanonicalName(itemidNode, out pvalue) == VSConstants.S_OK ? //pvalue : null;
                Path.Combine(solutionDirectoryName, pvalue) : null;
        }

        private void OpenTracker()
        {
            solutionDirectoryName = null;

            IVsSolution sol = (IVsSolution)_sccProvider.GetService(typeof(SVsSolution));

            if (sol != null)
            {
                string solutionDirectory, solutionFile, solutionUserOptions;
                solutionDirectoryName = (sol.GetSolutionInfo(out solutionDirectory, out solutionFile, out solutionUserOptions) == VSConstants.S_OK) ?
                    solutionDirectory : null;

                _statusTracker.Open(solutionDirectoryName);
            }

            Refresh();
        }

   
        private void CloseTracker()
        {
            _statusTracker.Close();
        }

        void _statusTracker_OnGitRepoChanged(object sender, EventArgs e)
        {
            Refresh();
        }

        internal void Refresh()
        {
            if (solutionDirectoryName != null)
            {
                IVsSolution sol = (IVsSolution)_sccProvider.GetService(typeof(SVsSolution));
                EnumHierarchyItems(sol as IVsHierarchy, VSConstants.VSITEMID_ROOT);
            }
        }

        private void EnumHierarchyItems(IVsHierarchy hierarchy, uint itemid)
        {
            int hr;
            IntPtr nestedHierarchyObj;
            uint nestedItemId;
            Guid hierGuid = typeof(IVsHierarchy).GUID;

            hr = hierarchy.GetNestedHierarchy(itemid, ref hierGuid, out nestedHierarchyObj, out nestedItemId);
            if (VSConstants.S_OK == hr && IntPtr.Zero != nestedHierarchyObj)
            {
                IVsHierarchy nestedHierarchy = Marshal.GetObjectForIUnknown(nestedHierarchyObj) as IVsHierarchy;
                Marshal.Release(nestedHierarchyObj);
                if (nestedHierarchy != null)
                {
                    EnumHierarchyItems(nestedHierarchy, nestedItemId);
                }
            }
            else
            {
                object pVar;

                processNodeFunc(hierarchy, itemid);

                hr = hierarchy.GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_FirstVisibleChild, out pVar);
                if (VSConstants.S_OK == hr)
                {
                    uint childId = GetItemId(pVar);
                    while (childId != VSConstants.VSITEMID_NIL)
                    {
                        EnumHierarchyItems(hierarchy, childId);
                        hr = hierarchy.GetProperty(childId, (int)__VSHPROPID.VSHPROPID_NextVisibleSibling, out pVar);
                        if (VSConstants.S_OK == hr)
                        {
                            childId = GetItemId(pVar);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the item id.
        /// </summary>
        /// <param name="pvar">VARIANT holding an itemid.</param>
        /// <returns>Item Id of the concerned node</returns>
        private uint GetItemId(object pvar)
        {
            if (pvar == null) return VSConstants.VSITEMID_NIL;
            if (pvar is int) return (uint)(int)pvar;
            if (pvar is uint) return (uint)pvar;
            if (pvar is short) return (uint)(short)pvar;
            if (pvar is ushort) return (uint)(ushort)pvar;
            if (pvar is long) return (uint)(long)pvar;
            return VSConstants.VSITEMID_NIL;
        }

        private void processNodeFunc(IVsHierarchy hierarchy, uint itemid)
        {
            object pVal, p2, p3;
            int hr = hierarchy.GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_ParentHierarchy, out pVal);
            hr = hierarchy.GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_Parent, out p2);
            hr = hierarchy.GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_Name, out p3);

            ////if (pVal == null || (int)p2 == -1)
            if (pVal == null)
            {
                //string fileName = GetFileName(hierarchy, itemid);
                //if (string.IsNullOrEmpty(fileName)) return;
                //VsStateIcon[] rgsiGlyphs = new VsStateIcon[1];
                //uint[] rgdwSccStatus = new uint[1];
                //GetSccGlyph(1, new string[] { fileName }, rgsiGlyphs, rgdwSccStatus);

                var sccProject2 = hierarchy as IVsSccProject2;
                if (sccProject2 != null)
                {
                    //hr = sccProject2.SccGlyphChanged(1, new uint[] { itemid }, rgsiGlyphs, rgdwSccStatus);
                    hr = sccProject2.SccGlyphChanged(0, null, null, null);
                }
                else
                {
                    string fileName = GetFileName(hierarchy, itemid);
                    if (string.IsNullOrEmpty(fileName)) return;
                    VsStateIcon[] rgsiGlyphs = new VsStateIcon[1];
                    uint[] rgdwSccStatus = new uint[1];
                    GetSccGlyph(1, new string[] { fileName }, rgsiGlyphs, rgdwSccStatus);
                     hr = hierarchy.SetProperty(itemid, (int)__VSHPROPID.VSHPROPID_StateIconIndex, rgsiGlyphs[0]);
                }
            }
        }
    }
}