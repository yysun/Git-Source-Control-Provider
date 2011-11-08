using System;
using System.IO;
using System.Linq;
using GitScc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;

namespace BasicSccProvider.Tests
{
    /// <summary>
    ///This is a test class for GitFileStatusTrackerTest and is intended
    ///to contain all GitFileStatusTrackerTest Unit Tests
    ///</summary>
    [TestClass()]
    public class GetFileStatusTest
    {
        protected string tempFolder;
        protected string tempFile;
        protected string[] lines;

        public GetFileStatusTest()
        {

        }

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

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion

        /// <summary>
        ///A test for GetFileStatus
        ///</summary>
        [TestMethod()]
        public void GetFileStatusTest1()
        {
            var stopwatch = new Stopwatch();

            string workingFolder = @"D:\Users\Public\My Projects Tryout\orchard-1.0.20\src";
            GitFileStatusTracker target = new GitFileStatusTracker(workingFolder);
            
            var list1 = new List<GitFileStatus>();
            var list2= new List<GitFileStatus>();

            var list = target.GetChangedFiles();
            stopwatch.Start();
            foreach (var f in list)
            {
                list1.Add(target.GetFileStatusNoCache(f.FileName));
            }
            stopwatch.Stop();
            Debug.WriteLine(list.Count() + ":" + stopwatch.ElapsedMilliseconds);

            stopwatch.Reset();
            stopwatch.Start();
            foreach (var f in list)
            {
                list2.Add(target.GetFileStatusNoCacheOld(f.FileName));
            }
            stopwatch.Stop();
            Debug.WriteLine(list.Count() + ":" + stopwatch.ElapsedMilliseconds);

            for(int i=0; i<list1.Count; i++)
            {
                Assert.AreEqual(list1[i], list2[i]);
            }
        }

        /// <summary>
        ///A test for GetChangedFiles
        ///2183:4123
        ///2183:28931
        ///2183:4361
        ///2183:8281
        ///</summary>
        [TestMethod()]
        public void GetChangedFilesTest1()
        {
            var stopwatch = new Stopwatch();

            string workingFolder = @"D:\Users\Public\My Projects Tryout\orchard-1.0.20\src";
            GitFileStatusTracker target = new GitFileStatusTracker(workingFolder);

            stopwatch.Start();
            var list = target.GetChangedFiles();
            stopwatch.Stop();
            Debug.WriteLine(list.Count() + ":" + stopwatch.ElapsedMilliseconds);

            stopwatch.Reset();
            stopwatch.Start();
            var changes = target.ChangedFiles;

            stopwatch.Stop();
            Debug.WriteLine(changes.Count() + ":" + stopwatch.ElapsedMilliseconds);

            Assert.AreEqual(list.Count(), changes.Count());

             GitBash.GitExePath = @"C:\Program Files (x86)\Git\bin\sh.exe";
            stopwatch.Reset();
            stopwatch.Start();
            GitBash.Run("status --porcelain -z --untracked-files", workingFolder);

            stopwatch.Stop();
            Debug.WriteLine(stopwatch.ElapsedMilliseconds);

        }
    }

}
