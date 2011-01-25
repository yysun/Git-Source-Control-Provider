using System;
using Microsoft.VisualStudio.OLE.Interop;
using System.Runtime.InteropServices;

namespace GitScc
{
    // ------------------------------------------------------------------------
    // IOleComponent OnIdle trigger
    // ------------------------------------------------------------------------
    public delegate void OnIdleEvent();

    class SccOnIdleEvent : IOleComponent
    {
        uint _wComponentID = 0;
        IOleComponentManager _cmService = null;

        public event OnIdleEvent OnIdleEvent;

        public void RegisterForIdleTimeCallbacks(IOleComponentManager cmService)
        {
            _cmService = cmService;

            if (_cmService != null)
            {
                OLECRINFO[] pcrinfo = new OLECRINFO[1];
                pcrinfo[0].cbSize = (uint)Marshal.SizeOf(typeof(OLECRINFO));
                pcrinfo[0].grfcrf = (uint)_OLECRF.olecrfNeedIdleTime |
                                              (uint)_OLECRF.olecrfNeedPeriodicIdleTime;
                pcrinfo[0].grfcadvf = (uint)_OLECADVF.olecadvfModal |
                                              (uint)_OLECADVF.olecadvfRedrawOff |
                                              (uint)_OLECADVF.olecadvfWarningsOff;
                pcrinfo[0].uIdleTimeInterval = 100;

                _cmService.FRegisterComponent(this, pcrinfo, out _wComponentID);
            }
        }

        public void UnRegisterForIdleTimeCallbacks()
        {
            if (_cmService != null)
                _cmService.FRevokeComponent(_wComponentID);
        }
        
        public virtual int FContinueMessageLoop(uint uReason, IntPtr pvLoopData, MSG[] pMsgPeeked)
        { return 1;  }

        /// <summary>
        /// Idle processing trigger method
        /// </summary>
        public virtual int FDoIdle(uint grfidlef)
        {
            if (OnIdleEvent != null)
                OnIdleEvent();

            return 0;
        }

        public virtual int FPreTranslateMessage(MSG[] pMsg)
        { return 0; }
        public virtual int FQueryTerminate(int fPromptUser)
        { return 1; }
        public virtual int FReserved1(uint dwReserved, uint message, IntPtr wParam, IntPtr lParam)
        { return 0; }
        public virtual IntPtr HwndGetWindow(uint dwWhich, uint dwReserved)
        { return IntPtr.Zero; }
        public virtual void OnActivationChange(IOleComponent pic, int fSameComponent, OLECRINFO[] pcrinfo, int fHostIsActivating, OLECHOSTINFO[] pchostinfo, uint dwReserved)
        { ; }
        public virtual void OnAppActivate(int fActive, uint dwOtherThreadID)
        { ; }
        public virtual void OnEnterState(uint uStateID, int fEnter)
        { ; }
        public virtual void OnLoseActivation()
        { ; }
        public virtual void Terminate()
        { ; }
    }
}
