using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace ThumbnailCreationDemo
{
	/// <summary>
	/// Demo code for how to create thumbnails of video and image files, using
	/// .NET Core 3.1, the StorageFile API and/or the executable of ffmpeg.
	/// </summary>
	class Program
	{
		// Change the constant to the location of the ffmpeg executable on your system:
		const string _FfmpegExeFilePath = @"C:\Program Files\FFmpeg\bin\ffmpeg2.exe";


		const uint _SizeDivider = 3;

		enum MediaType
		{
			Video,
			Image
		}

		struct ThumbnailSize
		{
			public uint Width;
			public uint Height;
		}

		static async Task Main(string[] args)
		{
			if (!File.Exists(_FfmpegExeFilePath))
			{
				Console.WriteLine("Please make sure that the \"_FfmpegExeFilePath\" constant");
				Console.WriteLine("contains the location of \"ffmpeg.exe\" on your system!");
				Console.WriteLine();
				Console.WriteLine($"Current value is \"{{_FfmpegExeFilePath}}\"");
				Console.ReadKey();
				return;
			}
				
			var mediaDirectoryPath = GetMediaDirectoryPath();

			var videoFile = await GetStorageFile(mediaDirectoryPath, @"Video.wmv");
			var videoProperties = await videoFile.Properties.GetVideoPropertiesAsync();
			var videoHeight = videoProperties.Height;
			var videoWidth = videoProperties.Width;
			await CreateThumbnailUsingStorageFile(videoFile, GetRequestedThumbnailSize(videoWidth, videoHeight));
			await CreateThumbnailUsingFfmpeg(videoFile.Path, GetThumbnailSize(videoWidth, videoHeight), MediaType.Video);

			var imageFile = await GetStorageFile(mediaDirectoryPath, @"Image.png");
			var imageProperties = await imageFile.Properties.GetImagePropertiesAsync();
			var imageHeight = imageProperties.Height;
			var imageWidth = imageProperties.Width;

			await CreateThumbnailUsingStorageFile(imageFile, GetRequestedThumbnailSize(imageWidth, imageHeight));
			await CreateThumbnailUsingFfmpeg(imageFile.Path, GetThumbnailSize(imageWidth, imageHeight), MediaType.Image);

			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("======================");
			Console.WriteLine($"Thumbnails created in \"{mediaDirectoryPath}\".");
			Console.WriteLine();
			Console.Write("Open Explorer (Y/N)? ");
			if (Console.ReadKey().Key == ConsoleKey.Y)
			{
				Process.Start("explorer", $"\"{mediaDirectoryPath}\"");
			}
		}

		static uint GetRequestedThumbnailSize(uint width, uint height)
		{
			return Math.Max(width, height) / _SizeDivider;
		}

		static ThumbnailSize GetThumbnailSize(uint width, uint height)
		{
			return new ThumbnailSize { Width = width / _SizeDivider, Height = height / _SizeDivider };
		}

		static string GetMediaDirectoryPath()
		{
			var exeFilePath = new Uri(Assembly.GetExecutingAssembly().Location).LocalPath;
			var exeFileDirectoryPath = Path.GetDirectoryName(exeFilePath);
			if (String.IsNullOrEmpty(exeFileDirectoryPath)) throw new Exception("Could not determine directory of EXE file");
			return Path.Combine(exeFileDirectoryPath, "media");
		}

		static async Task<StorageFile> GetStorageFile(string mediaDirectoryPath, string fileName)
		{
			var sourceFilePath = Path.Combine(mediaDirectoryPath, fileName);
			return await StorageFile.GetFileFromPathAsync(sourceFilePath);
		}

		static async Task CreateThumbnailUsingStorageFile(StorageFile storageFile, uint requestedSize)
		{
			var thumbnailFilePath = storageFile.Path + ".thumb1.png";
			var storageFolder = await storageFile.GetParentAsync();
			using StorageItemThumbnail thumbnail = await storageFile.GetThumbnailAsync(ThumbnailMode.SingleItem, requestedSize, ThumbnailOptions.ResizeThumbnail);

			var bitmapDecoder = await BitmapDecoder.CreateAsync(thumbnail.CloneStream());
			var softwareBitmap = await bitmapDecoder.GetSoftwareBitmapAsync();

			var thumbnailFile = await storageFolder.CreateFileAsync(Path.GetFileName(thumbnailFilePath), CreationCollisionOption.ReplaceExisting);
			using var stream = await thumbnailFile.OpenAsync(FileAccessMode.ReadWrite);
			var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
			encoder.SetSoftwareBitmap(softwareBitmap);
			await encoder.FlushAsync();
		}

		static Task CreateThumbnailUsingFfmpeg(string mediaFilePath, ThumbnailSize thumbnailSize, MediaType mediaType)
		{
			var thumbnailFilePath = mediaFilePath + ".thumb2.png";

			string seekParameter = (mediaType == MediaType.Video) ? "-ss 5" : "";
			var tcs = new TaskCompletionSource<int>();

			var process = new Process
			{
				StartInfo =
					{
						FileName = _FfmpegExeFilePath,
						Arguments = $"-i \"{mediaFilePath}\" -vframes 1 -an -s {thumbnailSize.Width}x{thumbnailSize.Height} {seekParameter}  -y \"{thumbnailFilePath}\""
					},
				EnableRaisingEvents = true
			};

			process.Exited += (sender, args) =>
			{
				tcs.SetResult(process.ExitCode);
				process.Dispose();
			};
			process.Start();
			return tcs.Task;
		}
	}
}
