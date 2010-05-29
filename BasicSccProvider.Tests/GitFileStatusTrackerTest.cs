using System;
using System.IO;
using GitScc;
using GitSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BasicSccProvider.Tests
{
    
    
    /// <summary>
    ///This is a test class for GitFileStatusTrackerTest and is intended
    ///to contain all GitFileStatusTrackerTest Unit Tests
    ///</summary>
    [TestClass()]
    public class GitFileStatusTrackerTest
    {


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
        ///A test for HasGitRepository
        ///</summary>
        [TestMethod()]
        public void HasGitRepositoryTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_";

            GitFileStatusTracker tracker = new GitFileStatusTracker();
            tracker.Open(tempFolder);
            Assert.IsFalse(tracker.HasGitRepository);

            Repository.Init(tempFolder);

            tracker.Open(tempFolder);
            Assert.IsTrue(tracker.HasGitRepository);
            
        }

        [TestMethod]
        public void GetFileStatusTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_";
            var tempFile = Path.Combine(tempFolder, "test");

            Repository.Init(tempFolder);
            GitFileStatusTracker tracker = new GitFileStatusTracker();
            tracker.Open(tempFolder);

            string[] lines = { "First line", "Second line", "Third line" };
            File.WriteAllLines(tempFile, lines);

            tracker.Update();
            Assert.AreEqual(GitFileStatus.UnTrackered, tracker.GetFileStatus(tempFile));

            using (var repo = new Repository(tempFolder))
            {
                repo.Index.Add(tempFile);

                tracker.Update();
                Assert.AreEqual(GitFileStatus.Staged, tracker.GetFileStatus(tempFile));

                repo.Index.CommitChanges("test", new Author("test", "test@test.test"));

                tracker.Update();
                Assert.AreEqual(GitFileStatus.Trackered, tracker.GetFileStatus(tempFile));

                repo.Close();
            }

            File.WriteAllText(tempFile, "changed text");
            
            tracker.Update();
            Assert.AreEqual(GitFileStatus.Modified, tracker.GetFileStatus(tempFile));
        }

        [TestMethod]
        public void GetFileContentTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_";
            var tempFile = Path.Combine(tempFolder, "test");

            Repository.Init(tempFolder);

            string[] lines = { "First line", "Second line", "Third line" };
            File.WriteAllLines(tempFile, lines);


            using (var repo = new Repository(tempFolder))
            {
                repo.Index.Add(tempFile);
                repo.Index.CommitChanges("test", new Author("test", "test@test.test"));
                repo.Close();
            }

            GitFileStatusTracker tracker = new GitFileStatusTracker();
            tracker.Open(tempFolder);

            var fileContent = tracker.GetFileContent(tempFile);

            using (var binWriter = new BinaryWriter(File.Open(tempFile + ".bk", FileMode.Create)))
            {              
                binWriter.Write(fileContent);
            }

            var newlines = File.ReadAllLines(tempFile + ".bk");
            Assert.AreEqual(lines[0], newlines[0]);
            Assert.AreEqual(lines[1], newlines[1]);
            Assert.AreEqual(lines[2], newlines[2]);
        }
    }
}
