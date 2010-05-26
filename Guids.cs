/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/

using System;

namespace GitScc
{
	/// <summary>
	/// This class is used only to expose the list of Guids used by this package.
	/// This list of guids must match the set of Guids used inside the VSCT file.
	/// </summary>
    static class GuidList
    {
		// Now define the list of guids as public static members.
   
        // Unique ID of the source control provider; this is also used as the command UI context to show/hide the pacakge UI
        public static readonly Guid guidSccProvider = new Guid("{C4128D99-0000-41D1-A6C3-704E6C1A3DE2}");
        // The guid of the source control provider service (implementing IVsSccProvider interface)
        public static readonly Guid guidSccProviderService = new Guid("{C4128D99-1000-41D1-A6C3-704E6C1A3DE2}");
        // The guid of the source control provider package (implementing IVsPackage interface)
        public static readonly Guid guidSccProviderPkg = new Guid("{C4128D99-2000-41D1-A6C3-704E6C1A3DE2}");

        //Other guids for menus and commands
        public static readonly Guid guidSccProviderCmdSet = new Guid("{C4A089DA-E640-438d-A977-815C267CA76D}");
                                                                       
    };
}
