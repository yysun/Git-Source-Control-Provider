using GitScc.DataServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Constants = NGit.Constants;

namespace BasicSccProvider.Tests
{
    
    /// <summary>
    ///This is a test class for RepositoryGraphTest and is intended
    ///to contain all RepositoryGraphTest Unit Tests
    ///</summary>
    [TestClass()]
    public class RepositoryGraphTest
    {
        private const string repodir = @"D:\Users\Public\My Projects Tryout\gitsharp\";

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

        //[TestMethod()]
        //public void TreeTest()
        //{
        //    RepositoryGraph repo = new RepositoryGraph(repodir);
        //    var tree = repo.GetTree("master");
        //    foreach (var t in tree.Trees)
        //    {
        //        Console.WriteLine("{0}:{1}", "Tree:", t.Name);
        //    }
        //    foreach (var b in tree.Blobs)
        //    {
        //        Console.WriteLine("{0}:{1}", "Blob:", b.Name);
        //    }
        //}

        [TestMethod()]
        public void TreeDiffTest()
        {
            RepositoryGraph repo = new RepositoryGraph(repodir);
            var changes = repo.GetChanges(Constants.MASTER, Constants.HEAD);
            foreach (var change in changes)
            {
                Console.WriteLine("{0}:{1}", change.ChangeType, change.Name);
            }
        }

        [TestMethod()]
        public void TreeDiffTest1()
        {
            RepositoryGraph repo = new RepositoryGraph(repodir);
            var changes = repo.GetChanges("master");
            foreach (var change in changes)
            {
                Console.WriteLine("{0}:{1}", change.ChangeType, change.Name);
            }
        }
    }
}
