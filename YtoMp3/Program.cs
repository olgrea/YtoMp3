using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YtoMp3
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Enter video or playlist url");
            }

            var url = args[0];

            var youtube = new YoutubeClient();
            var videoId = new VideoId(url);

            var streams = await youtube.Videos.Streams.GetManifestAsync(videoId);
            var streamInfo = streams.GetAudioOnly().WithHighestBitrate();
            if (streamInfo == null)
            {
                Console.Error.WriteLine("This videos has no streams");
            }
            
            var fileName = $"{videoId}.{streamInfo.Container.Name}";

            // Download video
            Console.Write($"Downloading stream: {streamInfo.Url} / {streamInfo.Container.Name}... ");
            using (var progress = new InlineProgress())
            {
                await youtube.Videos.Streams.DownloadAsync(streamInfo, fileName, progress);
            }

            Console.WriteLine($"Video saved to '{fileName}'");
        }
    }

    internal class InlineProgress : IProgress<double>, IDisposable
    {
        private readonly int _posX;
        private readonly int _posY;

        public InlineProgress()
        {
            _posX = Console.CursorLeft;
            _posY = Console.CursorTop;
        }

        public void Report(double progress)
        {
            Console.SetCursorPosition(_posX, _posY);
            Console.WriteLine($"{progress:P1}");
        }

        public void Dispose()
        {
            Console.SetCursorPosition(_posX, _posY);
            Console.WriteLine("Completed ✓");
        }
    }
}
