using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace YtoMp3
{
    public class Options
    {
        [Value(0, Required = true, HelpText = "The id, url or file path of the video/playlist. ")]
        public string Path { get; set; }
        
        [Option('m', Default = false, HelpText = "Merge videos of a playlist or a folder into a single mp3.")]
        public bool Merge { get; set; }
    }
    
    class Program
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

            await results.WithParsedAsync(YtoMp3.Execute);
        }
    }
}
