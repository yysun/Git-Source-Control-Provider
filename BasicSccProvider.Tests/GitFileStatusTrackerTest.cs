using System;
using System.IO;
using System.Linq;
using GitScc;
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

        [TestMethod()]
        public void GetRepositoryDirectoryTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_0";
            var newFolder = tempFolder + "\\t t";
            Directory.CreateDirectory(newFolder);
            GitFileStatusTracker.Init(tempFolder);
            var folder = GitFileStatusTracker.GetRepositoryDirectory(newFolder);
            Assert.AreEqual(tempFolder, folder);
        }

        [TestMethod()]
        public void HasGitRepositoryTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_1";
            GitFileStatusTracker.Init(tempFolder);
            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);

            Assert.IsTrue(tracker.HasGitRepository);
            Assert.AreEqual(tempFolder, tracker.GitWorkingDirectory);
            Assert.IsTrue(Directory.Exists(tempFolder + "\\.git"));
        }

        [TestMethod]
        public void GetFileStatusTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_2";
            var tempFile = Path.Combine(tempFolder, "test");

            GitFileStatusTracker.Init(tempFolder);
            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);

            string[] lines = { "First line", "Second line", "Third line" };

            File.WriteAllLines(tempFile, lines);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.New, tracker.GetFileStatus(tempFile));

            tracker.StageFile(tempFile);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Added, tracker.GetFileStatus(tempFile));

            tracker.Commit("test commit");
            Assert.AreEqual(GitFileStatus.Tracked, tracker.GetFileStatus(tempFile));

            File.WriteAllText(tempFile, "changed text");
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Modified, tracker.GetFileStatus(tempFile));

            File.Delete(tempFile);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Deleted, tracker.GetFileStatus(tempFile));

            tracker.RemoveFile(tempFile);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Removed, tracker.GetFileStatus(tempFile));
        }

        [TestMethod]
        public void GetFileContentTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_3";
            var tempFile = Path.Combine(tempFolder, "test");

            GitFileStatusTracker.Init(tempFolder);

            string[] lines = { "First line", "Second line", "Third line" };
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            tracker.StageFile(tempFile);
            tracker.Commit("test");

            var fileContent = tracker.GetFileContent(tempFile);

            using (var binWriter = new BinaryWriter(File.Open(tempFile + ".bk", System.IO.FileMode.Create)))
            {
                binWriter.Write(fileContent);
            }

            var newlines = File.ReadAllLines(tempFile + ".bk");
            Assert.AreEqual(lines[0], newlines[0]);
            Assert.AreEqual(lines[1], newlines[1]);
            Assert.AreEqual(lines[2], newlines[2]);
        }

        [TestMethod]
        public void GetChangedFilesTest()
        {

            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_5";
            var tempFile = Path.Combine(tempFolder, "test");

            GitFileStatusTracker.Init(tempFolder);

            string[] lines = { "First line", "Second line", "Third line" };
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            Assert.AreEqual(GitFileStatus.New, tracker.ChangedFiles.ToList()[0].Status);

            tracker.StageFile(tempFile);
            Assert.AreEqual(GitFileStatus.Added, tracker.ChangedFiles.ToList()[0].Status);

            tracker.Commit("test");
            
            Assert.AreEqual(0, tracker.ChangedFiles.Count());

            File.WriteAllText(tempFile, "a");
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Modified, tracker.ChangedFiles.ToList()[0].Status);

            tracker.StageFile(tempFile);
            Assert.AreEqual(GitFileStatus.Staged, tracker.ChangedFiles.ToList()[0].Status);
        }

        [TestMethod]
        public void LastCommitMessageTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_6";
            var tempFile = Path.Combine(tempFolder, "test");

            GitFileStatusTracker.Init(tempFolder);
            string[] lines = { "First line", "Second line", "Third line" };
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            tracker.StageFile(tempFile);
            
            tracker.Commit("test message");
            Assert.AreEqual("test message", tracker.LastCommitMessage);
        }

        [TestMethod]
        public void AmendCommitTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_7";
            var tempFile = Path.Combine(tempFolder, "test");

            GitFileStatusTracker.Init(tempFolder);
            string[] lines = { "First line", "Second line", "Third line" };
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            tracker.StageFile(tempFile);

            tracker.Commit("test message");
            Assert.AreEqual("test message", tracker.LastCommitMessage);

            File.WriteAllText(tempFile, "changed text");
            tracker.StageFile(tempFile);
            tracker.AmendCommit("new message");
            Assert.AreEqual("new message", tracker.LastCommitMessage);
        }

        [TestMethod]
        public void DiffFileTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_8";
            var tempFile = Path.Combine(tempFolder, "test");

            GitFileStatusTracker.Init(tempFolder);
            string[] lines = { "First line", "Second line", "Third line" };
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            tracker.StageFile(tempFile);

            tracker.Commit("test message");
            Assert.AreEqual("test message", tracker.LastCommitMessage);

            File.WriteAllText(tempFile, "changed text");
            var diff = tracker.DiffFile(tempFile);
            Console.WriteLine(diff);
            Assert.IsTrue(diff.StartsWith("@@ -1,3 +1 @@"));
        }
    }
}
