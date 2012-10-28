using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VSSDK.Tools.VsIdeTesting;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using EnvDTE;

namespace BasicSccProvider.Tests
{
    /// <summary>
    /// Integration test for package validation
    /// </summary>
    [TestClass]
    public class PackageTest
    {
        private delegate void ThreadInvoker();

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        [TestMethod]
        [HostType("VS IDE")]
        public void PackageLoadTest()
        {
            UIThreadInvoker.Invoke((ThreadInvoker)delegate()
            {

                //Get the Shell Service
                IVsShell shellService = VsIdeTestHostContext.ServiceProvider.GetService(typeof(SVsShell)) as IVsShell;
                Assert.IsNotNull(shellService);

                //Validate package load
                IVsPackage package;
                Guid packageGuid = new Guid("C4128D99-2000-41D1-A6C3-704E6C1A3DE2");
                Assert.IsTrue(0 == shellService.LoadPackage(ref packageGuid, out package));
                Assert.IsNotNull(package, "Package failed to load");

            });
        }


        /// <summary>
        /// Create project and check if the hasProjectOpened flag is set.
        /// </summary>
        [TestMethod()]
        [HostType("VS IDE")]
        public void TestHasProjectOpened()
        {
            UIThreadInvoker.Invoke((ThreadInvoker)delegate()
            {
                //Get the global service provider and the dte
                IServiceProvider sp = VsIdeTestHostContext.ServiceProvider;
                DTE dte = (DTE)sp.GetService(typeof(DTE));
                dte.Solution.Open(@"E:\Users\Public\My Projects\GitScc\Publish\TestProjects\UTF8Test\UTF8Test.sln");
            });
        }

    }
}
