using System;

namespace ThumbnailCreationDemo
{
	partial class Program
	{
		public class ThumbnailSize
		{
			public ThumbnailSize(uint originalWidth, uint originalHeight, uint sizeDivider)
			{
				Max = Math.Max(originalWidth, originalHeight) / _SizeDivider;
				Width = originalWidth / _SizeDivider;
				Height = originalHeight / _SizeDivider;
			}
			public uint Width;
			public uint Height;
			public uint Max;
		}
	}
}
