﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using MyYoutubeNow.Utils;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace MyYoutubeNow
{
    public class MyYoutubeNow
    {
        static async Task Main(string[] args)
        {
            var results = CommandLine.Parser.Default.ParseArguments<Options>(args);
            if (results.Errors.Any())
            {
                foreach (Error error in results.Errors) 
                    Console.WriteLine($"{error} - {error.Tag}");
                return;
            }

            await results.WithParsedAsync(MyYoutubeNow.Execute);
        }
        
        public static async Task Execute(Options options)
        {
            var client = new YoutubeClient();
            var converter = new Converter(); 
            
            if (VideoId.TryParse(options.Url) != null)
            {
                VideoId id = new VideoId(options.Url);
                var videoPath = await client.DownloadVideo(id);
                var mp3Path = await converter.ConvertToMp3(videoPath);
                if (File.Exists(videoPath)) 
                    File.Delete(videoPath);
                string temp = mp3Path;
            }
            else if(PlaylistId.TryParse(options.Url) != null)
            {
                PlaylistId id = new PlaylistId(options.Url);
                Playlist info = await client.GetPlaylistAsync(id);
                var videoPaths = await client.DownloadPlaylist(id, info);
                var outputDir = await converter.ConvertToMp3(videoPaths, $"{info.Title.RemoveInvalidChars()}", options.Concatenate);
                foreach (var path in videoPaths)
                {
                    if (File.Exists(path)) 
                        File.Delete(path);
                }
            }
        }
    }
}