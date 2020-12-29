using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace MyYoutubeNow
{
    internal class Converter
    {
        public async Task<string> ConvertToMp3(IEnumerable<string> pathsToConvert, string outputDirName = "output", bool concatenate = false)
        {
            if (!concatenate)
            {
                var list = pathsToConvert.ToList();
                for (var i = 0; i < list.Count; i++)
                {
                    Console.WriteLine($"{i}/{list.Count}");
                    await ConvertToMp3(list[i], outputDirName);
                }

                return Path.Combine(Directory.GetCurrentDirectory(), outputDirName);
            }
            
            return await ConcatenateMp3s(pathsToConvert, outputDirName);
        }
        
        public async Task<string> ConvertToMp3(string pathToConvert, string outputDirName = "output")
        {
            // TODO : set as embedded
            FFmpeg.SetExecutablesPath(Directory.GetCurrentDirectory());

            var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, outputDirName);
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

            Console.WriteLine($"{pathToConvert} will be converted to mp3");
            return await DoConversion(conversion);
        }

        internal async Task<string> ConcatenateMp3s(IEnumerable<string> pathsToMerge, string outputDirName = "output")
        {
            // TODO : set as embedded
            FFmpeg.SetExecutablesPath(Directory.GetCurrentDirectory());

            var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, outputDirName);
            Directory.CreateDirectory(outputDir);

            var filename = $"{Path.GetFileNameWithoutExtension(outputDirName)}.mp3";
            var filePath = Path.Combine(outputDir, filename);
            
            IConversion conversion = FFmpeg.Conversions.New();

            var concatParam = CreateConcatToMp3Param(pathsToMerge, filePath);
            conversion.AddParameter(concatParam)
                .SetOverwriteOutput(true)
                .SetOutput(filePath);
            
            Console.WriteLine($"{pathsToMerge.Count()} file will be concatenated to {filePath}.");
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

        public async Task<string> ConcatenateMp3sInFolder(string path)
        {
            var filepaths = Directory.EnumerateFiles(path, "*.mp3", SearchOption.TopDirectoryOnly);
            Console.WriteLine($"{filepaths.Count()} mp3 files in {path} will be concatenated");

            if (!path.EndsWith(Path.DirectorySeparatorChar)) 
                path += Path.DirectorySeparatorChar;

            return await ConvertToMp3(filepaths, Path.GetDirectoryName(path), true);
        }
        
        private static async Task<string> DoConversion(IConversion conversion)
        {
            Console.WriteLine($"Converting to {conversion.OutputFilePath} ...");

            var cmd = conversion.Build();
            var nbInputs = cmd.Split("-i").Count();
            
            using (var convProgress = new InlineProgress())
            {
                conversion.OnProgress += (sender, args) => { convProgress.Report((double) args.Percent / (nbInputs * 100d)); };
                await conversion.Start();
            }

            var outputFileInfo = new FileInfo(conversion.OutputFilePath);
            if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
                Console.Error.WriteLine($"problem during conversion of {outputFileInfo.Name}.");
            return conversion.OutputFilePath;
        }
    }
}