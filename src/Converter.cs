using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MyYoutubeNow.Utils;
using Xabe.FFmpeg;

namespace MyYoutubeNow
{
    internal class Converter
    {
        private const string FfmpegReleaseUrl = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases";
        private readonly string _ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        private readonly string _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        private string _tempPath;
        private string TempPath
        {
            get
            {
                if (string.IsNullOrEmpty(_tempPath))
                {
                    _tempPath = Path.GetTempPath();
                    Directory.CreateDirectory(_tempPath);
                }

                return _tempPath;
            }
        }
        
        public Converter()
        {
            if (!File.Exists(_ffmpegPath))
            {
                Console.WriteLine("FFmpeg not found.");
                var t = DownloadFFmpeg();
                t.Wait();
            }
            FFmpeg.SetExecutablesPath(_baseDirectory);
        }

        ~Converter()
        {
            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, true);
            }
        }

        private async Task DownloadFFmpeg()
        {
            Console.WriteLine("Downloading FFmpeg...");
            using HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "request");

            var json = await httpClient.GetStringAsync(FfmpegReleaseUrl);
            using JsonDocument doc = JsonDocument.Parse(json);
            var jsonDocument = doc.RootElement.Clone();
            // ReSharper disable once HeapView.BoxingAllocation
            var elem = jsonDocument
                .EnumerateArray().First()
                .GetProperty("assets")
                .EnumerateArray()
                .FirstOrDefault(item => item.GetProperty("browser_download_url").GetString().EndsWith("win64-gpl.zip"));

            var releaseUrl = elem.GetProperty("browser_download_url").GetString();
            var zipFileName = Path.GetFileName(releaseUrl);
            var zipPath = Path.Combine(_baseDirectory, zipFileName);

            using (var progress = new InlineProgress())
            {
                await httpClient.DownloadAsync(releaseUrl, zipPath, elem.GetProperty("size").GetInt64(), progress);
            }
            
            var extractedDir = zipPath.Replace(".zip", "");
            if (Directory.Exists(extractedDir)) 
                Directory.Delete(extractedDir, true);
            
            await using (FileStream zipStream = File.OpenRead(zipPath))
            {
                var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
                zip.ExtractToDirectory(_baseDirectory);
            }
            
            foreach (string file in Directory.EnumerateFiles(extractedDir, "*.exe", SearchOption.AllDirectories))
            {
                File.Copy(file, Path.Combine(_baseDirectory, Path.GetFileName(file)));
            }

            if (File.Exists(_ffmpegPath))
            {
                File.Delete(zipPath);
                Directory.Delete(extractedDir, true);
            }
        }

        public async Task<string> ConvertToMp3(IEnumerable<string> pathsToConvert, string outputDirName = "output")
        {
            var list = pathsToConvert.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                Console.WriteLine($"Conversion {i+1}/{list.Count}");
                await ConvertToMp3(list[i], outputDirName);
            }

            return Path.Combine(Directory.GetCurrentDirectory(), outputDirName);
        }

        public async Task<string> ConvertToMp3s(string pathToSplit, IEnumerable<Chapter> chapters, string outputDirName = "output")
        {
            var tempDir = pathToSplit.Replace(Path.GetFileName(pathToSplit), "");

            var chapterList = chapters.ToList();
            for (int i = 0; i < chapterList.Count - 1; i++)
            {
                var chapter = chapterList[i];
                var partPath = Path.Combine(_baseDirectory, outputDirName, chapter.Title + ".mp3");
                
                IConversion conversion = FFmpeg.Conversions.New();
                var end = i + 1 != chapterList.Count - 1 ? chapterList[i + 1].TimeRangeStart : 0;
                
                IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(pathToSplit);
                var audioStream = mediaInfo.AudioStreams.FirstOrDefault()?.SetCodec(AudioCodec.mp3);

                conversion.AddStream(audioStream)
                    .SetOverwriteOutput(true)
                    .SetOutput(partPath)
                    .AddParameter(CreateSplitParameter(chapter.TimeRangeStart, end))
                    .AddParameter(CreateFadeOutParameter())
                    ;

                var b = conversion.Build();
                Console.WriteLine($"Converting chapter {chapter.Title} to mp3...");
                await DoConversion(conversion);
            }

            Directory.Delete(tempDir, true);
            return outputDirName;
        }

        private static string CreateSplitParameter(ulong start, ulong end)
        {
            var sb = new StringBuilder();
            sb.Append($"-ss {TimeSpan.FromMilliseconds(start).ToFFmpeg()} ");
            if (end > 0)
            {
                sb.Append($"-to {TimeSpan.FromMilliseconds(end).ToFFmpeg()} ");
            }
            
            return sb.ToString();
        }

        private static string CreateFadeOutParameter()
        {
            return "-filter_complex \"aevalsrc=0:d=1.0 [a_silence]; [0:a:0] [a_silence] acrossfade=d=1.0\" ";
        }

        public async Task<string> ConvertToMp3(string pathToConvert, string outputDirName = "output")
        {
            var outputDir = Path.Combine(_baseDirectory, outputDirName);
            Directory.CreateDirectory(outputDir);

            IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(pathToConvert);
            var audioStream = mediaInfo.AudioStreams.FirstOrDefault()?.SetCodec(AudioCodec.mp3);

            var filename = $"{Path.GetFileNameWithoutExtension(pathToConvert)}.mp3";
            var filePath = Path.Combine(outputDir, filename);

            IConversion conversion = FFmpeg.Conversions.New();
            conversion.AddStream(audioStream)
                .SetOverwriteOutput(true)
                .SetOutput(filePath)
                .SetAudioBitrate(audioStream.Bitrate);

            Console.WriteLine($"Converting {Path.GetFileNameWithoutExtension(filePath)} to mp3...");
            return await DoConversion(conversion);
        }

        public async Task<string> ConcatenateMp3s(IEnumerable<string> pathsToMerge, string outputDirName = "output")
        {
            var outputDir = Path.Combine(_baseDirectory, outputDirName);
            Directory.CreateDirectory(outputDir);

            var filename = $"{Path.GetFileNameWithoutExtension(outputDirName)}.mp3";
            var filePath = Path.Combine(outputDir, filename);
            
            IConversion conversion = FFmpeg.Conversions.New();

            var concatParam = CreateConcatToMp3Param(pathsToMerge, filePath);
            conversion.AddParameter(concatParam)
                .SetOverwriteOutput(true)
                .SetOutput(filePath);
            
            Console.WriteLine($"Concatenating {pathsToMerge.Count()} files...");
            return await DoConversion(conversion);
        }
        
        private static string CreateConcatToMp3Param(IEnumerable<string> pathsToMerge, string outputFilePath)
        {
            var sb = new StringBuilder();
            var count = pathsToMerge.Count();

            // Input parameters
            foreach (var path in pathsToMerge)
                sb.Append($"-i \"{path}\" ");

            // filter
            sb.Append($"-filter_complex \"");
            for (int i = 0; i < count; i++)
                sb.Append($"[{i}:a:0]");
            sb.Append($"concat=n={count}:v=0:a=1[outa]\" ");

            // map
            sb.Append($"-map \"[outa]\" \"{outputFilePath}\" ");
            return sb.ToString();
        }
        
        private static async Task<string> DoConversion(IConversion conversion)
        {
            var cmd = conversion.Build();
            var nbInputs = cmd.Split("-i" ).Count() - 1;

            using var convProgress = new InlineProgress();
            conversion.OnProgress += (sender, args) => { convProgress.Report((double) args.Percent / nbInputs); };
            await conversion.Start();

            var outputFileInfo = new FileInfo(conversion.OutputFilePath);
            if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
                Console.Error.WriteLine($"problem during conversion of {outputFileInfo.Name}.");
            return conversion.OutputFilePath;
        }
    }
}