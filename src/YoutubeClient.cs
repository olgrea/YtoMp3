using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Dom;
using CommandLine;
using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using AngleSharp.Html.Dom;
using MyYoutubeNow.Utils;

namespace MyYoutubeNow
{
    public class Options
    {
        [Value(0, Required = true, HelpText = "The url of the video/playlist. ")]
        public string Url { get; set; }
        
        [Option('s', Default = false, HelpText = "Split a video with chapters and convert them into mp3s.")]
        public bool Split { get; set; }
        
        [Option('c', Hidden = true, Default = false, HelpText = "Concatenate videos of a playlist into a single mp3.")]
        public bool Concatenate { get; set; }
    }

    public class Chapter
    {
        public string Title { get; }
        public ulong TimeRangeStart { get; }
        public Chapter(string title, ulong timeRangeStart)
        {
            Title = title;
            TimeRangeStart = timeRangeStart;
        }
    }

    internal class YoutubeClient
    {
        private YoutubeExplode.YoutubeClient _client;

        private string _tempPath;
        private string TempPath
        {
            get
            {
                if (string.IsNullOrEmpty(_tempPath))
                {
                    _tempPath = Path.GetTempPath();
                }

                return _tempPath;
            }
        }
        
        public YoutubeClient()
        {
            _client = new YoutubeExplode.YoutubeClient();
        }

        public async Task<Video> GetVideoInfoAsync(VideoId id)
        {
            return await _client.Videos.GetAsync(id);
        }
        
        public async Task<Playlist> GetPlaylistInfoAsync(PlaylistId id)
        {
            return await _client.Playlists.GetAsync(id);
        }
        
        public async Task<string> DownloadVideo(VideoId id, Video videoInfo = null)
        {
            videoInfo ??= await _client.Videos.GetAsync(id);
            StreamManifest manifest = await _client.Videos.Streams.GetManifestAsync(id);
            Console.WriteLine($"Downloading video {videoInfo.Title}...");
            
            if (manifest == null)
                throw new ArgumentException("no manifest found");

            var stream = manifest.GetAudioOnly().WithHighestBitrate();
            if (stream == null)
                throw new ArgumentException("no audio stream found");

            var tempDir = Path.Combine(TempPath, Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            
            var videoPath = Path.Combine(tempDir, $"{videoInfo.Title.RemoveInvalidChars()}.{stream.Container.Name}");
            
            using (var progress = new  InlineProgress())
            {
                await _client.Videos.Streams.DownloadAsync(stream, videoPath, progress);
            }

            return videoPath;
        }

        public async Task<IEnumerable<string>> DownloadPlaylist(PlaylistId id, Playlist info = null)
        {
            info ??= await _client.Playlists.GetAsync(id);
            var videos = await _client.Playlists.GetVideosAsync(id);
            Console.WriteLine($"{videos.Count} videos found in playlist {info.Title}");
            var videoPaths = new List<string>();
            for (var i = 0; i < videos.Count; i++)
            {
                Console.WriteLine($"{i+1}/{videos.Count}");
                videoPaths.Add(await DownloadVideo(videos[i].Url));
            }

            return videoPaths;
        }
        
        internal async Task<List<Chapter>> GetChaptersAsync(VideoId videoId)
        {
            try
            {
                var watchPageDoc = await GetHtmlWatchPage(videoId);
                return TryGetChapters(watchPageDoc);
            }
            catch { }

            try
            {
                return await TryGetChaptersFromDescription(videoId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Getting chapters failed");
                Console.WriteLine(ex.Message);                
                throw;
            }
            
            //TODO : try parse chapters comments
        }

        private async Task<List<Chapter>> TryGetChaptersFromDescription(VideoId id)
        {
            Video videoInfo = await _client.Videos.GetAsync(id);
            var desc = videoInfo.Description;
            var regex = new Regex("[0-9]?[0-9]?:?[0-5]?[0-9]:[0-5][0-9]", RegexOptions.Compiled);

            var descLines = desc.Split("\n", StringSplitOptions.RemoveEmptyEntries);

            var chapters = new List<Chapter>();
            foreach (string line in descLines)
            {
                var match = regex.Match(line);
                if (match.Success)
                {
                    var title = line.Replace(match.Value, "").TrimStart().TrimEnd();
                    var matchValue = match.Value;
                    if (matchValue.Count(c => c == ':') == 1) 
                        matchValue = "00:" + matchValue;

                    var startTime = (ulong)TimeSpan.Parse(matchValue).TotalMilliseconds;
                    chapters.Add(new Chapter(title, startTime));
                }
            }
            
            return chapters;
        }
        
        private static List<Chapter> TryGetChapters(IHtmlDocument watchPageDoc)
        {
            var ytInitialData = watchPageDoc
                .GetElementsByTagName("script")
                .Select(e => e.Text())
                .FirstOrDefault(s => s.Contains("ytInitialData"));

            var json = Regex.Match(ytInitialData, "ytInitialData\\s*=\\s*(.+?})(?:\"\\))?;", RegexOptions.Singleline).Groups[1]
                .Value;

            using var doc = JsonDocument.Parse(json);
            var jsonDocument = doc.RootElement.Clone();
            // ReSharper disable once HeapView.BoxingAllocation
            var chaptersArray = jsonDocument
                .GetProperty("playerOverlays")
                .GetProperty("playerOverlayRenderer")
                .GetProperty("decoratedPlayerBarRenderer")
                .GetProperty("decoratedPlayerBarRenderer")
                .GetProperty("playerBar")
                .GetProperty("chapteredPlayerBarRenderer")
                .GetProperty("chapters")
                .EnumerateArray()
                .Select(j => new Chapter(
                    j.GetProperty("chapterRenderer").GetProperty("title").GetProperty("simpleText").GetString(),
                    j.GetProperty("chapterRenderer").GetProperty("timeRangeStartMillis").GetUInt64()));

            return chaptersArray.ToList();
        }

        private async Task<IHtmlDocument> GetHtmlWatchPage(VideoId videoId)
        {
            var assembly = typeof(YoutubeExplode.YoutubeClient).Assembly;
            var httpClient = typeof(VideoClient).GetField("_httpClient",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_client.Videos);

            var watchPageObj = assembly.GetType("YoutubeExplode.ReverseEngineering.Responses.WatchPage");
            var methodInfo = watchPageObj.GetMethod("GetAsync");

            var watchPage = await methodInfo.InvokeAsync(null, new object[] {httpClient, videoId.ToString()});

            var root = (IHtmlDocument) watchPage.GetType().GetField("_root", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(watchPage);
            return root;
        }
    }
}
