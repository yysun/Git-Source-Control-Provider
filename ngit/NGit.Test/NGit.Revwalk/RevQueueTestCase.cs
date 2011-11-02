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

using NGit.Revwalk;
using Sharpen;

namespace NGit.Revwalk
{
	public abstract class RevQueueTestCase<T> : RevWalkTestCase where T:AbstractRevQueue
	{
		protected internal T q;

		/// <exception cref="System.Exception"></exception>
		public override void SetUp()
		{
			base.SetUp();
			q = Create();
		}

		protected internal abstract T Create();

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestEmpty()
		{
			NUnit.Framework.Assert.IsNull(q.Next());
			NUnit.Framework.Assert.IsTrue(q.EverbodyHasFlag(RevWalk.UNINTERESTING));
			NUnit.Framework.Assert.IsFalse(q.AnybodyHasFlag(RevWalk.UNINTERESTING));
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestClear()
		{
			RevCommit a = ParseBody(Commit());
			RevCommit b = ParseBody(Commit(a));
			q.Add(a);
			q.Add(b);
			q.Clear();
			NUnit.Framework.Assert.IsNull(q.Next());
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestHasFlags()
		{
			RevCommit a = ParseBody(Commit());
			RevCommit b = ParseBody(Commit(a));
			q.Add(a);
			q.Add(b);
			NUnit.Framework.Assert.IsFalse(q.EverbodyHasFlag(RevWalk.UNINTERESTING));
			NUnit.Framework.Assert.IsFalse(q.AnybodyHasFlag(RevWalk.UNINTERESTING));
			a.flags |= RevWalk.UNINTERESTING;
			NUnit.Framework.Assert.IsFalse(q.EverbodyHasFlag(RevWalk.UNINTERESTING));
			NUnit.Framework.Assert.IsTrue(q.AnybodyHasFlag(RevWalk.UNINTERESTING));
			b.flags |= RevWalk.UNINTERESTING;
			NUnit.Framework.Assert.IsTrue(q.EverbodyHasFlag(RevWalk.UNINTERESTING));
			NUnit.Framework.Assert.IsTrue(q.AnybodyHasFlag(RevWalk.UNINTERESTING));
		}
	}
}
