using System;
using System.IO;
using GitScc;
using GitSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Diagnostics;

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
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_0";

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            Assert.IsFalse(tracker.HasGitRepository);

            Repository.Init(tempFolder);

            tracker = new GitFileStatusTracker(tempFolder);
            Assert.IsTrue(tracker.HasGitRepository);
            
        }

        [TestMethod]
        public void GetFileStatusTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_1";
            var tempFile = Path.Combine(tempFolder, "test");

            Repository.Init(tempFolder);
            File.WriteAllText(Path.Combine(tempFolder, ".gitignore"), "*.txt");

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);
            
            string[] lines = { "First line", "Second line", "Third line" };
 
            Debug.WriteLine("==== Create file " + System.Threading.Thread.CurrentThread.GetHashCode());
            File.WriteAllLines(tempFile, lines);
            tracker.Refresh();
            Debug.WriteLine("==== ");
            Assert.AreEqual(GitFileStatus.New, tracker.GetFileStatus(tempFile));

            using (var repo = new Repository(tempFolder))
            {
                Debug.WriteLine("==== Add file " + System.Threading.Thread.CurrentThread.GetHashCode());
                repo.Index.Add(tempFile);
                tracker.Refresh();

                Debug.WriteLine("==== ");
                Assert.AreEqual(GitFileStatus.Added, tracker.GetFileStatus(tempFile));

                Debug.WriteLine("==== Commit file " + System.Threading.Thread.CurrentThread.GetHashCode());
                repo.Index.CommitChanges("test", new Author("test", "test@test.test"));
                tracker.Refresh();
                Debug.WriteLine("==== ");
                Assert.AreEqual(GitFileStatus.Trackered, tracker.GetFileStatus(tempFile));

                Debug.WriteLine("==== Change file " + System.Threading.Thread.CurrentThread.GetHashCode());
                File.WriteAllText(tempFile, "changed text");
                tracker.Refresh();
                Debug.WriteLine("==== ");
                Assert.AreEqual(GitFileStatus.Modified, tracker.GetFileStatus(tempFile));

                Debug.WriteLine("==== Delete file " + System.Threading.Thread.CurrentThread.GetHashCode());
                File.Delete(tempFile);
                tracker.Refresh();
                Debug.WriteLine("==== ");
                Assert.AreEqual(GitFileStatus.Missing, tracker.GetFileStatus(tempFile));

                Debug.WriteLine("==== Commit deletion " + System.Threading.Thread.CurrentThread.GetHashCode());
                repo.Index.Remove(tempFile);
                tracker.Refresh();
                Debug.WriteLine("==== ");
                Assert.AreEqual(GitFileStatus.Removed, tracker.GetFileStatus(tempFile));

                repo.Close();
            }

            Debug.WriteLine("==== Create Ingore file " + System.Threading.Thread.CurrentThread.GetHashCode());
            File.WriteAllLines(tempFile + ".txt", lines);
            Thread.Sleep(200);
            Debug.WriteLine("==== ");
            Assert.AreEqual(GitFileStatus.Ignored, tracker.GetFileStatus(tempFile + ".txt"));

        }

        //[TestMethod]
        //public void GitMonitorFileTest()
        //{
        //    var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_2";
        //    var tempFile = Path.Combine(tempFolder, "test");

        //    Repository.Init(tempFolder);
        //    GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);

        //    string[] lines = { "First line", "Second line", "Third line" };

        //    File.WriteAllLines(tempFile, lines);
        //    Assert.IsFalse(File.Exists(tracker.GitMonitorFile));

        //    using (var repo = new Repository(tempFolder))
        //    {
        //        repo.Index.Add(tempFile);
        //        Assert.IsFalse(File.Exists(tracker.GitMonitorFile));

        //        repo.Index.CommitChanges("test", new Author("test", "test@test.test"));
        //        Assert.IsFalse(File.Exists(tracker.GitMonitorFile));

        //        File.WriteAllText(tempFile, "changed text");
        //        Assert.IsFalse(File.Exists(tracker.GitMonitorFile));

        //        File.Delete(tempFile);
        //        Assert.IsFalse(File.Exists(tracker.GitMonitorFile));

        //        repo.Index.Remove(tempFile);
        //        Assert.IsFalse(File.Exists(tracker.GitMonitorFile));

        //        repo.Close();
        //    }

        //    Thread.Sleep(1000);
        //    Assert.IsTrue(File.Exists(tracker.GitMonitorFile));
        //}

        [TestMethod]
        public void GetFileContentTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_3";
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

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);

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

        [TestMethod]
        public void GitIgnoreFileTest()
        {
            var tempFolder = Environment.CurrentDirectory + "\\_gitscc_test_4";
            var tempFile = Path.Combine(tempFolder, "test.txt.other.t");

            Repository.Init(tempFolder);
            File.WriteAllText(Path.Combine(tempFolder, ".gitignore"), "*.t");

            GitFileStatusTracker tracker = new GitFileStatusTracker(tempFolder);

            string[] lines = { "First line", "Second line", "Third line" };
            File.WriteAllLines(tempFile, lines);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Ignored, tracker.GetFileStatus(tempFile));

        }
    }
}
