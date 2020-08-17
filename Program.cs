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
	/// .NET Core 3.1, the StorageFile API and/or the executable of FFmpeg.
	/// </summary>
	partial class Program
	{
		// Change the constant to the location of the FFmpeg executable on your system:
		const string _FfmpegExeFilePath = @"C:\Program Files\FFmpeg\bin\ffmpeg.exe";

		const uint _SizeDivider = 3; // Thumbnails to have 1/3 of the original size

		static async Task Main(string[] args)
		{
			if (!File.Exists(_FfmpegExeFilePath))
			{
				Console.WriteLine("Please make sure that the \"_FfmpegExeFilePath\" constant");
				Console.WriteLine("contains the location of \"ffmpeg.exe\" on your system!");
				Console.WriteLine();
				Console.WriteLine($"Current value is \"{_FfmpegExeFilePath}\"");
				Console.ReadKey();
				return;
			}

			var mediaDirectoryPath = GetMediaDirectoryPath();

			var videoFile = await GetStorageFile(mediaDirectoryPath, @"Video.wmv");
			var videoProperties = await videoFile.Properties.GetVideoPropertiesAsync();
			var videoThumbnailSize = new ThumbnailSize(videoProperties.Width, videoProperties.Height, _SizeDivider);

			var millisecondsVideo1 = await CreateThumbnailUsingStorageFile(videoFile, videoThumbnailSize);
			var millisecondsVideo2 = await CreateThumbnailUsingFfmpeg(videoFile.Path, videoThumbnailSize, MediaType.Video);

			var imageFile = await GetStorageFile(mediaDirectoryPath, @"Image.png");
			var imageProperties = await imageFile.Properties.GetImagePropertiesAsync();
			var imageThumbnailSize = new ThumbnailSize(imageProperties.Width, imageProperties.Height, _SizeDivider);

			var millisecondsImage1 = await CreateThumbnailUsingStorageFile(imageFile, imageThumbnailSize);
			var millisecondsImage2 = await CreateThumbnailUsingFfmpeg(imageFile.Path, imageThumbnailSize, MediaType.Image);

			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("======================");
			Console.WriteLine($"Video (StorageFile): {millisecondsVideo1} ms");
			Console.WriteLine($"Video (FFmpeg)     : {millisecondsVideo2} ms");
			Console.WriteLine($"Image (StorageFile): {millisecondsImage1} ms");
			Console.WriteLine($"Image (FFmpeg)     : {millisecondsImage2} ms");
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

		static async Task<long> CreateThumbnailUsingStorageFile(StorageFile storageFile, ThumbnailSize thumbnailSize)
		{
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			var thumbnailFilePath = storageFile.Path + ".thumb1.png";
			var storageFolder = await storageFile.GetParentAsync();
			using StorageItemThumbnail thumbnail = await storageFile.GetThumbnailAsync(ThumbnailMode.SingleItem, thumbnailSize.Max, ThumbnailOptions.ResizeThumbnail);

			var bitmapDecoder = await BitmapDecoder.CreateAsync(thumbnail.CloneStream());
			var softwareBitmap = await bitmapDecoder.GetSoftwareBitmapAsync();

			var thumbnailFile = await storageFolder.CreateFileAsync(Path.GetFileName(thumbnailFilePath), CreationCollisionOption.ReplaceExisting);
			using var stream = await thumbnailFile.OpenAsync(FileAccessMode.ReadWrite);
			var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
			encoder.SetSoftwareBitmap(softwareBitmap);
			await encoder.FlushAsync();

			stopwatch.Stop();
			return stopwatch.ElapsedMilliseconds;
		}

		static Task<long> CreateThumbnailUsingFfmpeg(string mediaFilePath, ThumbnailSize thumbnailSize, MediaType mediaType)
		{
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			var thumbnailFilePath = mediaFilePath + ".thumb2.png";

			string additionalVideoParameters = (mediaType == MediaType.Video) ? "-ss 5 -an" : "";
			var tcs = new TaskCompletionSource<long>();

			var process = new Process
			{
				StartInfo =
					{
						FileName = _FfmpegExeFilePath,
						Arguments = $"{additionalVideoParameters} -i \"{mediaFilePath}\" -vframes 1 -s {thumbnailSize.Width}x{thumbnailSize.Height} -y \"{thumbnailFilePath}\""
					},
				EnableRaisingEvents = true // so the "Exited" event will get raised when the process has finished
			};

			process.Exited += (sender, args) =>
			{
				process.Dispose();
				stopwatch.Stop();
				tcs.SetResult(stopwatch.ElapsedMilliseconds);
			};
			process.Start();
			return tcs.Task;
		}
	}
}
