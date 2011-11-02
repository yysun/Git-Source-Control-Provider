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
using NGit.Junit;
using NGit.Storage.File;
using Sharpen;

namespace NGit.Storage.File
{
	[NUnit.Framework.TestFixture]
	public class T0004_PackReaderTest : SampleDataRepositoryTestCase
	{
		private static readonly string PACK_NAME = "pack-34be9032ac282b11fa9babdc2b2a93ca996c9c2f";

		private static readonly FilePath TEST_PACK = JGitTestUtil.GetTestResourceFile(PACK_NAME
			 + ".pack");

		private static readonly FilePath TEST_IDX = JGitTestUtil.GetTestResourceFile(PACK_NAME
			 + ".idx");

		/// <exception cref="System.IO.IOException"></exception>
		[NUnit.Framework.Test]
		public virtual void Test003_lookupCompressedObject()
		{
			PackFile pr;
			ObjectId id;
			ObjectLoader or;
			id = ObjectId.FromString("902d5476fa249b7abc9d84c611577a81381f0327");
			pr = new PackFile(TEST_IDX, TEST_PACK);
			or = pr.Get(new WindowCursor(null), id);
			NUnit.Framework.Assert.IsNotNull(or);
			NUnit.Framework.Assert.AreEqual(Constants.OBJ_TREE, or.GetType());
			NUnit.Framework.Assert.AreEqual(35, or.GetSize());
			pr.Close();
		}

		/// <exception cref="System.IO.IOException"></exception>
		[NUnit.Framework.Test]
		public virtual void Test004_lookupDeltifiedObject()
		{
			ObjectId id;
			ObjectLoader or;
			id = ObjectId.FromString("5b6e7c66c276e7610d4a73c70ec1a1f7c1003259");
			or = db.Open(id);
			NUnit.Framework.Assert.IsNotNull(or);
			NUnit.Framework.Assert.AreEqual(Constants.OBJ_BLOB, or.GetType());
			NUnit.Framework.Assert.AreEqual(18009, or.GetSize());
		}
	}
}
