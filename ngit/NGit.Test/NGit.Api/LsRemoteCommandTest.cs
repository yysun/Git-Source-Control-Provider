/*
This code is derived from jgit (http://eclipse.org/jgit).
Copyright owners are documented in jgit's IP log.

This program and the accompanying materials are made available
under the terms of the Eclipse Distribution License v1.0 which
accompanies this distribution, is reproduced below, and is
available at http://www.eclipse.org/org/documents/edl-v10.php

All rights reserved.

Redistribution and use in source and binary forms, with or
without modification, are permitted provided that the following
conditions are met:

- Redistributions of source code must retain the above copyright
  notice, this list of conditions and the following disclaimer.

- Redistributions in binary form must reproduce the above
  copyright notice, this list of conditions and the following
  disclaimer in the documentation and/or other materials provided
  with the distribution.

- Neither the name of the Eclipse Foundation, Inc. nor the
  names of its contributors may be used to endorse or promote
  products derived from this software without specific prior
  written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using NGit;
using NGit.Api;
using Sharpen;

namespace NGit.Api
{
	[NUnit.Framework.TestFixture]
	public class LsRemoteCommandTest : RepositoryTestCase
	{
		private Git git;

		/// <exception cref="System.Exception"></exception>
		public override void SetUp()
		{
			base.SetUp();
			git = new Git(db);
			// commit something
			WriteTrashFile("Test.txt", "Hello world");
			git.Add().AddFilepattern("Test.txt").Call();
			git.Commit().SetMessage("Initial commit").Call();
			// create a master branch and switch to it
			git.BranchCreate().SetName("test").Call();
			RefUpdate rup = db.UpdateRef(Constants.HEAD);
			rup.Link("refs/heads/test");
			// tags
			git.Tag().SetName("tag1").Call();
			git.Tag().SetName("tag2").Call();
			git.Tag().SetName("tag3").Call();
		}

		[NUnit.Framework.Test]
		public virtual void TestLsRemote()
		{
			try
			{
				FilePath directory = CreateTempDirectory("testRepository");
				CloneCommand command = Git.CloneRepository();
				command.SetDirectory(directory);
				command.SetURI("file://" + git.GetRepository().WorkTree.GetPath());
				command.SetCloneAllBranches(true);
				Git git2 = command.Call();
				AddRepoToClose(git2.GetRepository());
				LsRemoteCommand lsRemoteCommand = git2.LsRemote();
				ICollection<Ref> refs = lsRemoteCommand.Call();
				NUnit.Framework.Assert.IsNotNull(refs);
				NUnit.Framework.Assert.AreEqual(6, refs.Count);
			}
			catch (Exception e)
			{
				NUnit.Framework.Assert.Fail(e.Message);
			}
		}

		[NUnit.Framework.Test]
		public virtual void TestLsRemoteWithTags()
		{
			try
			{
				FilePath directory = CreateTempDirectory("testRepository");
				CloneCommand command = Git.CloneRepository();
				command.SetDirectory(directory);
				command.SetURI("file://" + git.GetRepository().WorkTree.GetPath());
				command.SetCloneAllBranches(true);
				Git git2 = command.Call();
				AddRepoToClose(git2.GetRepository());
				LsRemoteCommand lsRemoteCommand = git2.LsRemote();
				lsRemoteCommand.SetTags(true);
				ICollection<Ref> refs = lsRemoteCommand.Call();
				NUnit.Framework.Assert.IsNotNull(refs);
				NUnit.Framework.Assert.AreEqual(3, refs.Count);
			}
			catch (Exception e)
			{
				NUnit.Framework.Assert.Fail(e.Message);
			}
		}

		[NUnit.Framework.Test]
		public virtual void TestLsRemoteWithHeads()
		{
			try
			{
				FilePath directory = CreateTempDirectory("testRepository");
				CloneCommand command = Git.CloneRepository();
				command.SetDirectory(directory);
				command.SetURI("file://" + git.GetRepository().WorkTree.GetPath());
				command.SetCloneAllBranches(true);
				Git git2 = command.Call();
				AddRepoToClose(git2.GetRepository());
				LsRemoteCommand lsRemoteCommand = git2.LsRemote();
				lsRemoteCommand.SetHeads(true);
				ICollection<Ref> refs = lsRemoteCommand.Call();
				NUnit.Framework.Assert.IsNotNull(refs);
				NUnit.Framework.Assert.AreEqual(2, refs.Count);
			}
			catch (Exception e)
			{
				NUnit.Framework.Assert.Fail(e.Message);
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public static FilePath CreateTempDirectory(string name)
		{
			FilePath temp;
			temp = FilePath.CreateTempFile(name, System.Convert.ToString(Runtime.NanoTime()));
			if (!(temp.Delete()))
			{
				throw new IOException("Could not delete temp file: " + temp.GetAbsolutePath());
			}
			if (!(temp.Mkdir()))
			{
				throw new IOException("Could not create temp directory: " + temp.GetAbsolutePath(
					));
			}
			return temp;
		}
	}
}
