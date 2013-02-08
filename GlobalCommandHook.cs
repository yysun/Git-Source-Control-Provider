namespace GitScc
{
    using System;
    using System.Collections.Generic;
    using CommandID = System.ComponentModel.Design.CommandID;
    using ErrorHandler = Microsoft.VisualStudio.ErrorHandler;
    using IOleCommandTarget = Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget;
    using IVsRegisterPriorityCommandTarget = Microsoft.VisualStudio.Shell.Interop.IVsRegisterPriorityCommandTarget;
    using OLECMD = Microsoft.VisualStudio.OLE.Interop.OLECMD;
    using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
    using SVsRegisterPriorityCommandTarget = Microsoft.VisualStudio.Shell.Interop.SVsRegisterPriorityCommandTarget;

    internal sealed class GlobalCommandHook : IOleCommandTarget
    {
        private static GlobalCommandHook _instance;

        private readonly BasicSccProvider _provider;
        private readonly Dictionary<Guid, Dictionary<int, EventHandler>> _commandMap = new Dictionary<Guid, Dictionary<int, EventHandler>>();
        private bool _hooked;
        private uint _cookie;

        private GlobalCommandHook(BasicSccProvider provider)
        {
            _provider = provider;
        }

        public static GlobalCommandHook GetInstance(BasicSccProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");

            if (_instance == null)
                _instance = new GlobalCommandHook(provider);

            return _instance;
        }

        public void HookCommand(CommandID command, EventHandler handler)
        {
            if (command == null)
                throw new ArgumentNullException("command");
            else if (handler == null)
                throw new ArgumentNullException("handler");

            Dictionary<int, EventHandler> map;
            if (!_commandMap.TryGetValue(command.Guid, out map))
            {
                map = new Dictionary<int, EventHandler>();
                _commandMap[command.Guid] = map;
            }

            EventHandler handlers;
            if (!map.TryGetValue(command.ID, out handlers))
                handlers = null;

            map[command.ID] = (handlers + handler);

            if (!_hooked)
            {
                IVsRegisterPriorityCommandTarget svc = (IVsRegisterPriorityCommandTarget)_provider.GetService(typeof(SVsRegisterPriorityCommandTarget));
                if (svc != null && ErrorHandler.Succeeded(svc.RegisterPriorityCommandTarget(0, this, out _cookie)))
                    _hooked = true;
            }
        }

        public void UnhookCommand(CommandID command, EventHandler handler)
        {
            if (command == null)
                throw new ArgumentNullException("command");
            else if (handler == null)
                throw new ArgumentNullException("handler");

            Dictionary<int, EventHandler> map;
            if (!_commandMap.TryGetValue(command.Guid, out map))
                return;

            EventHandler handlers;
            if (!map.TryGetValue(command.ID, out handlers))
                return;

            handlers -= handler;

            if (handlers == null)
            {
                map.Remove(command.ID);

                if (map.Count == 0)
                {
                    _commandMap.Remove(command.Guid);

                    if (_commandMap.Count == 0)
                    {
                        Unhook();
                    }
                }

                return;
            }

            map[command.ID] = (handlers + handler);
        }

        private void Unhook()
        {
            if (_hooked)
            {
                _hooked = false;
                ((IVsRegisterPriorityCommandTarget)_provider.GetService(typeof(SVsRegisterPriorityCommandTarget))).UnregisterPriorityCommandTarget(_cookie);
            }
        }

        private static bool GuidRefIsNull(ref Guid pguidCmdGroup)
        {
            // According to MSDN the Guid for the command group can be null and in this case the default
            // command group should be used. Given the interop definition of IOleCommandTarget, the only way
            // to detect a null guid is to try to access it and catch the NullReferenceExeption.
            Guid commandGroup;
            try
            {
                commandGroup = pguidCmdGroup;
            }
            catch (NullReferenceException)
            {
                // Here we assume that the only reason for the exception is a null guidGroup.
                // We do not handle the default command group as definied in the spec for IOleCommandTarget,
                // so we have to return OLECMDERR_E_NOTSUPPORTED.
                return true;
            }

            return false;
        }

        int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            // Exit as quickly as possible without creating exceptions. This function is performance critical!

            if (GuidRefIsNull(ref pguidCmdGroup))
                return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;

            Dictionary<int, EventHandler> cmdMap;
            if (!_commandMap.TryGetValue(pguidCmdGroup, out cmdMap))
                return (int)OleConstants.OLECMDERR_E_UNKNOWNGROUP;

            EventHandler handler;
            if (cmdMap.TryGetValue(unchecked((int)nCmdID), out handler))
            {
                handler(this, EventArgs.Empty);
            }

            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            // Don't do anything here. This function is 100% performance critical!
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }
    }
}
