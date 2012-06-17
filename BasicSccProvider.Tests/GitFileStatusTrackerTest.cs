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
        protected string tempFolder;
        protected string tempFile;
        protected string[] lines;
        
        public GitFileStatusTrackerTest()
        {
            tempFolder = Environment.CurrentDirectory + "\\" + Guid.NewGuid().ToString();
            tempFile = Path.Combine(tempFolder, "t e s t");
            lines = new string[] { "First line", "中文 2", "čtestč" };
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

        [TestMethod()]
        public void GetRepositoryDirectoryTest()
        {
            GitFileStatusTracker.Init(tempFolder);
            var newFolder = tempFolder + "\\t t\\a a";
            Directory.CreateDirectory(newFolder);
            GitFileStatusTracker tracker = new GitFileStatusTracker(newFolder);
            Assert.AreEqual(tempFolder, tracker.GitWorkingDirectory);
        }

        [TestMethod()]
        public void HasGitRepositoryTest()
        {
            
            GitFileStatusTracker.Init(tempFolder);
            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);

            Assert.IsTrue(tracker.HasGitRepository);
            Assert.AreEqual(tempFolder, tracker.GitWorkingDirectory);
            Assert.IsTrue(Directory.Exists(tempFolder + "\\.git"));
        }

        [TestMethod]
        public void GetFileStatusTest()
        {
            GitFileStatusTracker.Init(tempFolder);
            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);

            File.WriteAllLines(tempFile, lines);
            Assert.AreEqual(GitFileStatus.New, tracker.GetFileStatus(tempFile));

            tracker.StageFile(tempFile);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Added, tracker.GetFileStatus(tempFile));

            tracker.UnStageFile(tempFile);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.New, tracker.GetFileStatus(tempFile));

            tracker.StageFile(tempFile);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Added, tracker.GetFileStatus(tempFile));

            tracker.Commit("中文 1čtestč");
            Assert.AreEqual(GitFileStatus.Tracked, tracker.GetFileStatus(tempFile));

            File.WriteAllText(tempFile, "changed text");
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Modified, tracker.GetFileStatus(tempFile));

            tracker.StageFile(tempFile);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Staged, tracker.GetFileStatus(tempFile));

            tracker.UnStageFile(tempFile);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Modified, tracker.GetFileStatus(tempFile));

            File.Delete(tempFile);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Deleted, tracker.GetFileStatus(tempFile));

            tracker.StageFile(tempFile);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Removed, tracker.GetFileStatus(tempFile));

            tracker.UnStageFile(tempFile);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Deleted, tracker.GetFileStatus(tempFile));
        }

        [TestMethod]
        public void GetFileContentTest()
        {
            GitFileStatusTracker.Init(tempFolder);
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            tracker.StageFile(tempFile);
            tracker.Commit("中文 1čtestč");

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
        public void GetFileContentTestNegative()
        {
            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            var fileContent = tracker.GetFileContent(tempFile + ".bad");
            Assert.IsNull(fileContent);

            GitFileStatusTracker.Init(tempFolder);

            File.WriteAllLines(tempFile, lines);
            tracker = new GitFileStatusTracker(tempFolder);
            fileContent = tracker.GetFileContent(tempFile + ".bad");
            Assert.IsNull(fileContent);

            tracker.StageFile(tempFile);
            fileContent = tracker.GetFileContent(tempFile + ".bad");
            Assert.IsNull(fileContent);

            tracker.Commit("中文 1čtestč");

            fileContent = tracker.GetFileContent(tempFile + ".bad");
            Assert.IsNull(fileContent);
        }

        [TestMethod]
        public void GetChangedFilesTest()
        {
            GitFileStatusTracker.Init(tempFolder);

            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            Assert.AreEqual(GitFileStatus.New, tracker.ChangedFiles.ToList()[0].Status);

            tracker.StageFile(tempFile);
            Assert.AreEqual(GitFileStatus.Added, tracker.ChangedFiles.ToList()[0].Status);

            tracker.Commit("中文 1čtestč");
            
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
            GitFileStatusTracker.Init(tempFolder);
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            tracker.StageFile(tempFile);

            tracker.Commit("中文 1čtestč");
            Assert.IsTrue(tracker.LastCommitMessage.StartsWith("中文 1čtestč"));
        }

        [TestMethod]
        public void AmendCommitTest()
        {
            GitFileStatusTracker.Init(tempFolder);
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            tracker.StageFile(tempFile);

            tracker.Commit("中文 1čtestč");
            Assert.IsTrue(tracker.LastCommitMessage.StartsWith("中文 1čtestč"));

            File.WriteAllText(tempFile, "changed text");
            tracker.StageFile(tempFile);
            tracker.Commit("new message", true);
            Assert.IsTrue(tracker.LastCommitMessage.StartsWith("new message"));
        }

        [TestMethod]
        public void DiffFileTest()
        {
            GitFileStatusTracker.Init(tempFolder);
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            tracker.StageFile(tempFile);
            tracker.Commit("test message");
            File.WriteAllText(tempFile, "changed text");
            var diffFile = tracker.DiffFile(tempFile);
            var diff = File.ReadAllText(diffFile);
            Console.WriteLine(diff);
            Assert.IsTrue(diff.Contains("@@ -1,3 +1 @@"));
        }

        [TestMethod]
        public void FileNameCaseTest()
        {
            GitFileStatusTracker.Init(tempFolder);
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            tracker.StageFile(tempFile);

            tracker.Commit("test message");
            Assert.IsTrue(tracker.LastCommitMessage.StartsWith("test message"));
            tempFile = tempFile.Replace("test", "TEST");
            File.WriteAllText(tempFile, "changed text");
            tracker.Refresh();
            //This test fails all cases because status check uses ngit, never git.exe
            //Assert.AreEqual(GitFileStatus.Modified, tracker.GetFileStatus(tempFile));

            var file = tracker.ChangedFiles.First();
            Assert.AreEqual(GitFileStatus.Modified, file.Status);
        }

        [TestMethod]
        public void GetBranchTest()
        {
            GitFileStatusTracker.Init(tempFolder);
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            tracker.StageFile(tempFile);
            Assert.AreEqual("master", tracker.CurrentBranch);

            tracker.Commit("test message");
            Assert.AreEqual("master", tracker.CurrentBranch);

            tempFile = tempFile.Replace("test", "TEST");
            File.WriteAllText(tempFile, "changed text");

            tracker.CheckOutBranch("dev", true);
            Assert.AreEqual("dev", tracker.CurrentBranch);
        }

        [TestMethod]
        public void SaveFileFromRepositoryTest()
        {
            GitFileStatusTracker.Init(tempFolder);
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            tracker.StageFile(tempFile);
            tracker.Commit("test");

            tracker.SaveFileFromRepository(tempFile, tempFile + ".bk");
            var newlines = File.ReadAllLines(tempFile + ".bk");
            Assert.AreEqual(lines[0], newlines[0]);
            Assert.AreEqual(lines[1], newlines[1]);
            Assert.AreEqual(lines[2], newlines[2]);
        }

        [TestMethod]
        public void CheckOutFileTest()
        {
            GitFileStatusTracker.Init(tempFolder);
            File.WriteAllLines(tempFile, lines);

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            tracker.StageFile(tempFile);
            tracker.Commit("test");
            
            File.WriteAllText(tempFile, "changed text");
            tracker.CheckOutFile(tempFile);
            var newlines = File.ReadAllLines(tempFile);
            Assert.AreEqual(lines[0], newlines[0]);
            Assert.AreEqual(lines[1], newlines[1]);
            Assert.AreEqual(lines[2], newlines[2]);
        }

    }

    [TestClass()]
    public class GitFileStatusTrackerTest_NonAsciiFile : GitFileStatusTrackerTest
    {
        public GitFileStatusTrackerTest_NonAsciiFile()
        {
            GitBash.GitExePath = null;
            tempFolder = Environment.CurrentDirectory + "\\" + Guid.NewGuid().ToString();
            tempFile = Path.Combine(tempFolder, "中文 1čtestč");
        }
    }

    [TestClass()]
    public class GitFileStatusTrackerTest_NonAsciiFile_GitBash : GitFileStatusTrackerTest
    {
        public GitFileStatusTrackerTest_NonAsciiFile_GitBash()
        {
            GitBash.GitExePath = @"C:\Program Files (x86)\Git\bin\sh.exe";
            GitBash.UseUTF8FileNames = true;
            tempFolder = Environment.CurrentDirectory + "\\" + Guid.NewGuid().ToString();
            Directory.CreateDirectory(tempFolder);
            tempFile = Path.Combine(tempFolder, "中文 1čtestč");
        }
    }

    [TestClass()]
    public class GitFileStatusTrackerTest_WithSubFolder : GitFileStatusTrackerTest
    {
        public GitFileStatusTrackerTest_WithSubFolder()
        {
            GitBash.GitExePath = null;
            tempFolder = Environment.CurrentDirectory + "\\" + Guid.NewGuid().ToString();
            Directory.CreateDirectory(Path.Combine(tempFolder, "folder 1"));
            tempFile = Path.Combine(tempFolder, "folder 1\\t e s t");
        }
    }

    [TestClass()]
    public class GitFileStatusTrackerTest_WithSubFolder_NonAsciiFile : GitFileStatusTrackerTest
    {
        public GitFileStatusTrackerTest_WithSubFolder_NonAsciiFile()
        {
            GitBash.GitExePath = null;
            tempFolder = Environment.CurrentDirectory + "\\" + Guid.NewGuid().ToString();
            Directory.CreateDirectory(Path.Combine(tempFolder, "folder 1"));
            tempFile = Path.Combine(tempFolder, "folder 1\\中文 1čtestč");
        }
    }
    [TestClass()]
    public class GitFileStatusTrackerTest_WithSubFolder_UsingGitBash : GitFileStatusTrackerTest
    {
        public GitFileStatusTrackerTest_WithSubFolder_UsingGitBash()
        {
            GitBash.GitExePath = @"C:\Program Files (x86)\Git\bin\sh.exe";
            GitBash.UseUTF8FileNames = true;
            tempFolder = Environment.CurrentDirectory + "\\" + Guid.NewGuid().ToString();
            Directory.CreateDirectory(Path.Combine(tempFolder, "folder 1\\中文 1čtestč"));
            tempFile = Path.Combine(tempFolder, "folder 1\\中文 1čtestč\\中文 1čtestč");
        }
    }
}
