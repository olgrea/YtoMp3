using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var videoPath = await Download(idOrUrl);
            var mp3Path = await ConvertToMp3(videoPath);
            if (File.Exists(videoPath)) 
                File.Delete(videoPath);
            return mp3Path;
        }
        
        async Task<string> Download(string idOrUrl)
        {
            VideoId id = new VideoId(idOrUrl);

            Video videoInfo;
            try
            {
                videoInfo = await _youtube.Videos.GetAsync(id);
            }
            catch (Exception e) 
            {
                Console.WriteLine(e);
                throw;
            }
            var manifest = await _youtube.Videos.Streams.GetManifestAsync(id);
            if (manifest == null)
                throw new ArgumentException("no manifest found");

            var stream = manifest.GetAudioOnly().WithHighestBitrate();
            if (stream == null)
                throw new ArgumentException("no audio stream found");

            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            
            var videoPath = Path.Combine(tempDir, $"{RemoveInvalidChars(videoInfo.Title)}.{stream.Container.Name}");
            
            Console.WriteLine($"Downloading video {id}...");
            using (var progress = new  InlineProgress())
            {
                await _youtube.Videos.Streams.DownloadAsync(stream, videoPath, progress);
            }

            return videoPath;
        }

        private async Task<string> ConvertToMp3(string path)
        {
            return await ConvertToMp3(new[] {path});
        }
        
        private async Task<string> ConvertToMp3(IEnumerable<string> pathsToConvert)
        {
            var pathToConvert = pathsToConvert.First();
            
            // TODO : set as embedded
            FFmpeg.SetExecutablesPath(Directory.GetCurrentDirectory());

            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
            Directory.CreateDirectory(outputDir);
            
            var filename = $"{Path.GetFileNameWithoutExtension(pathToConvert)}.mp3";
            var filePath = Path.Combine(outputDir, filename);
            if (File.Exists(filePath))
                File.Delete(filePath);
            
            var mediaInfo = await FFmpeg.GetMediaInfo(pathToConvert);
            var audioStream = mediaInfo.AudioStreams.FirstOrDefault();

            var conversion = FFmpeg.Conversions.New();
            conversion.AddStream(audioStream);
            conversion.SetAudioBitrate(audioStream.Bitrate);
            conversion.SetOutput(filename);

            Console.WriteLine($"Converting {pathToConvert} to mp3...");
            using (var convProgress = new InlineProgress())
            {
                conversion.OnProgress += (sender, args) => { convProgress.Report((double)args.Percent/100d); };
                await conversion.Start();
            }

            var outputFileInfo = new FileInfo(pathToConvert);
            if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
                throw new ArgumentException($"problem during conversion of video {pathToConvert}.");

            return filename;
        }

        public async Task DownloadPlaylist(string idOrUrl, bool merge)
        {
            PlaylistId id = new PlaylistId(idOrUrl);
            Playlist info = await _youtube.Playlists.GetAsync(id);

            var videos = await _youtube.Playlists.GetVideosAsync(id);
            Console.WriteLine($"{videos.Count} videos found in playlist {info.Title}");
            foreach (var video in videos)
            {
                await Download(video.Url);
            }
        }
        
    }
}
