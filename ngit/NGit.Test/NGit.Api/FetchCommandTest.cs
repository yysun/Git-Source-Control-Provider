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

using NGit;
using NGit.Api;
using NGit.Revwalk;
using NGit.Storage.File;
using NGit.Transport;
using Sharpen;

namespace NGit.Api
{
	[NUnit.Framework.TestFixture]
	public class FetchCommandTest : RepositoryTestCase
	{
		/// <exception cref="NGit.Api.Errors.JGitInternalException"></exception>
		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="NGit.Api.Errors.GitAPIException"></exception>
		/// <exception cref="Sharpen.URISyntaxException"></exception>
		[NUnit.Framework.Test]
		public virtual void TestFetch()
		{
			// create other repository
			Repository db2 = CreateWorkRepository();
			Git git2 = new Git(db2);
			// setup the first repository to fetch from the second repository
			StoredConfig config = ((FileBasedConfig)db.GetConfig());
			RemoteConfig remoteConfig = new RemoteConfig(config, "test");
			URIish uri = new URIish(db2.Directory.ToURI().ToURL());
			remoteConfig.AddURI(uri);
			remoteConfig.Update(config);
			config.Save();
			// create some refs via commits and tag
			RevCommit commit = git2.Commit().SetMessage("initial commit").Call();
			RevTag tag = git2.Tag().SetName("tag").Call();
			Git git1 = new Git(db);
			RefSpec spec = new RefSpec("refs/heads/master:refs/heads/x");
			git1.Fetch().SetRemote("test").SetRefSpecs(spec).Call();
			NUnit.Framework.Assert.AreEqual(commit.Id, db.Resolve(commit.Id.GetName() + "^{commit}"
				));
			NUnit.Framework.Assert.AreEqual(tag.Id, db.Resolve(tag.Id.GetName()));
		}
	}
}
