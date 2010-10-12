using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Diagnostics;
using GitSharp.Core;
using GitScc;

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

            tracker.Commit("test");
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Tracked, tracker.GetFileStatus(tempFile));

            File.WriteAllText(tempFile, "changed text");
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Modified, tracker.GetFileStatus(tempFile));

            File.Delete(tempFile);
            tracker.Refresh();
            Assert.AreEqual(GitFileStatus.Missing, tracker.GetFileStatus(tempFile));

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
            var initFolder = @"E:\Users\Public\My Projects\GitScc\Publish\TestProjects\Folder Test";
            Repository repository = Repository.Open(initFolder);

            var commit = repository.MapCommit("HEAD");
            var tree = (commit != null ? commit.TreeEntry : new Tree(repository));
            var index = repository.Index;
            index.RereadIfNecessary();

            DirectoryInfo root = new DirectoryInfo(initFolder);
            var visitor = new AbstractIndexTreeVisitor { VisitEntryAux = OnVisitEntry };

            IgnoreHandler = new IgnoreHandler(repository);
            var mainTree = new Tree(repository);

            // Create the subdir branch
            foreach (string sdir in initFolder.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                mainTree = mainTree.AddTree(sdir);

            if (root.Exists)
                FillTree(root, mainTree);

            //new IndexTreeWalker(index, tree, mainTree, root, visitor).Walk();

        }

        IgnoreHandler IgnoreHandler;

        private void FillTree(DirectoryInfo dir, Tree tree)
        {
            foreach (var subdir in dir.GetDirectories())
            {
                if (subdir.Name == Constants.DOT_GIT || IgnoreHandler.IsIgnored(tree.FullName + "/" + subdir.Name))
                    continue;
                var t = tree.AddTree(subdir.Name);
                FillTree(subdir, t);
            }
            foreach (var file in dir.GetFiles())
            {
                if (IgnoreHandler.IsIgnored(tree.FullName + "/" + file.Name))
                    continue;
                tree.AddFile(file.Name);

                Console.WriteLine("{0}", file.Name);
            }
        }        

        private void OnVisitEntry(TreeEntry treeEntry, TreeEntry wdirEntry, GitIndex.Entry indexEntry, FileInfo file)
        {
            Console.WriteLine("{0} - {1} - {2} - {3}", file.Name, treeEntry, wdirEntry, indexEntry);
        }

        [TestMethod]
        public void GetChangedFilesTest_Core()
        {

        }
    }
}
