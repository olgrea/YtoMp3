using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using YoutubeExplode;
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

        public string ReplaceInvalidChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        public async Task Download(string idOrUrl)
        {
            VideoId id = new VideoId(idOrUrl);

            var videoInfo = await _youtube.Videos.GetAsync(id);
            var manifest = await _youtube.Videos.Streams.GetManifestAsync(id);
            if (manifest == null)
            {
                throw new ArgumentException("no manifest found");
            }

            var stream = manifest.GetAudioOnly().WithHighestBitrate();
            if (stream == null)
            {
                throw new ArgumentException("no audio stream found");
            }

            var tmpFile = $"{id.Value}.{stream.Container.Name}";
            if (File.Exists(tmpFile))
                File.Delete(tmpFile);

            Console.WriteLine($"Downloading video {id}...");
            using (var progress = new  InlineProgress())
            {
                await _youtube.Videos.Streams.DownloadAsync(stream, tmpFile, progress);
            }

            var tmpFileInfo = new FileInfo(tmpFile);
            if (!tmpFileInfo.Exists || tmpFileInfo.Length == 0)
            {
                throw new ArgumentException($"problem during download of video {id}.");
            }

            // TODO : set as embedded
            FFmpeg.SetExecutablesPath(Directory.GetCurrentDirectory());

            var mediaInfo = await FFmpeg.GetMediaInfo(tmpFile);
            var outputFilename = ReplaceInvalidChars($"{videoInfo.Title}.mp3");
            if (File.Exists(outputFilename))
                File.Delete(outputFilename);

            var audioStream = mediaInfo.AudioStreams.FirstOrDefault();

            var conversion = FFmpeg.Conversions.New();
            conversion.AddStream(audioStream);
            conversion.SetAudioBitrate(audioStream.Bitrate);
            conversion.SetOutput(outputFilename);

            Console.WriteLine($"Converting {id} to mp3...");
            var convProgress = new InlineProgress();
            conversion.OnProgress += (sender, args) => { convProgress.Report(args.Percent); };
            await conversion.Start();
            convProgress.Dispose();

            var outputFileInfo = new FileInfo(outputFilename);
            if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
            {
                throw new ArgumentException($"problem during conversion of video {id}.");
            }

            if (File.Exists(tmpFile))
                File.Delete(tmpFile);
        }
    }
}
