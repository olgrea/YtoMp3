using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Xabe.FFmpeg;
using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YtoMp3
{
    public class YtoMp3
    {
        private YoutubeClient _youtube;

        public YtoMp3()
        {
            _youtube = new YoutubeClient();
        }

        string RemoveInvalidChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        public async Task<string> ConvertVideo(string idOrUrl)
        {
            VideoId id = new VideoId(idOrUrl);
            var videoPath = await DownloadVideo(id);
            var mp3Path = await ConvertToMp3(videoPath);
            if (File.Exists(videoPath)) 
                File.Delete(videoPath);
            return mp3Path;
        }
        
        public async Task<string> ConvertPlaylist(string idOrUrl, bool merge)
        {
            PlaylistId id = new PlaylistId(idOrUrl);
            Playlist info = await _youtube.Playlists.GetAsync(id);
            var videoPaths = await DownloadPlaylist(id, info);
            var outputDir = await ConvertToMp3(videoPaths, $"{RemoveInvalidChars(info.Title)}", merge);
            foreach (var path in videoPaths)
            {
                if (File.Exists(path)) 
                    File.Delete(path);
            }
            
            return outputDir;
        }
        
        async Task<string> DownloadVideo(VideoId id, StreamManifest manifest = null)
        {
            Video videoInfo = await _youtube.Videos.GetAsync(id);
            manifest ??= await _youtube.Videos.Streams.GetManifestAsync(id);
            
            if (manifest == null)
                throw new ArgumentException("no manifest found");

            var stream = manifest.GetAudioOnly().WithHighestBitrate();
            if (stream == null)
                throw new ArgumentException("no audio stream found");

            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            
            var videoPath = Path.Combine(tempDir, $"{RemoveInvalidChars(videoInfo.Title)}.{stream.Container.Name}");
            
            Console.WriteLine($"Downloading video {videoInfo.Title}...");
            using (var progress = new  InlineProgress())
            {
                await _youtube.Videos.Streams.DownloadAsync(stream, videoPath, progress);
            }

            return videoPath;
        }

        async Task<IEnumerable<string>> DownloadPlaylist(PlaylistId id, Playlist info = null)
        {
            info ??= await _youtube.Playlists.GetAsync(id);
            var videos = await _youtube.Playlists.GetVideosAsync(id);
            Console.WriteLine($"{videos.Count} videos found in playlist {info.Title}");
            var videoPaths = new List<string>();
            foreach (var video in videos)
            {
                videoPaths.Add(await DownloadVideo(video.Url));
            }

            return videoPaths;
        }

        private async Task<string> ConvertToMp3(IEnumerable<string> pathsToConvert, string outputDirName = "output", bool merge = false)
        {
            if (!merge)
            {
                foreach (var path in pathsToConvert) 
                    await ConvertToMp3(path, outputDirName);
                return Path.Combine(Directory.GetCurrentDirectory(), outputDirName);    
            }
            
            return await MergeToMp3(pathsToConvert, outputDirName);
        }

        private async Task<string> MergeToMp3(IEnumerable<string> pathsToMerge, string outputDirName = "output")
        {
            // TODO : set as embedded
            FFmpeg.SetExecutablesPath(Directory.GetCurrentDirectory());

            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), outputDirName);
            Directory.CreateDirectory(outputDir);

            var filename = $"{Path.GetFileNameWithoutExtension(outputDirName)}.mp3";
            var filePath = Path.Combine(outputDir, filename);
            
            IConversion conversion = FFmpeg.Conversions.New();

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
            sb.Append($"-map \"[outa]\" \"{filePath}\" ");

            conversion.AddParameter(sb.ToString())
                .SetOverwriteOutput(true)
                .SetOutput(filePath);

            var p = conversion.Build();
            
            return await DoConversion(conversion);
        }
        
        private async Task<string> ConvertToMp3(string pathToConvert, string outputDirName = "output")
        {
            // TODO : set as embedded
            FFmpeg.SetExecutablesPath(Directory.GetCurrentDirectory());

            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), outputDirName);
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

            return await DoConversion(conversion);
        }

        private static async Task<string> DoConversion(IConversion conversion)
        {
            Console.WriteLine($"Converting to {conversion.OutputFilePath}...");
            using (var convProgress = new InlineProgress())
            {
                conversion.OnProgress += (sender, args) => { convProgress.Report((double) args.Percent / 100d); };
                await conversion.Start();
            }

            var outputFileInfo = new FileInfo(conversion.OutputFilePath);
            if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
                Console.Error.WriteLine($"problem during conversion of {outputFileInfo.Name}.");
            return conversion.OutputFilePath;
        }
    }
}
