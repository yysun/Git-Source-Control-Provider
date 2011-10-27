namespace Sharpen
{
	using System;
	using System.IO;

	internal class FileWriter : StreamWriter
	{
		public FileWriter (FilePath path) : base(path.GetPath ())
		{
		}
	}
}
