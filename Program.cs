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
