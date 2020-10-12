using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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

            var client = new YtoMp3();
            await client.Download(url);
        }
    }
}
