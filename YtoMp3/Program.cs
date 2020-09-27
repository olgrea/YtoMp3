using System;
using System.Reflection;
using System.Text;

namespace YtoMp3
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new YoutubeClient();
            var info = client.GetVideoInfo("https://www.youtube.com/watch?v=fKvk3tpHrdA&list=PL1qgThHfu0PbSTChu1bFaLV2-ijaAZCDY&index=44").Result;

            PrintInfo(info);
        }

        private static void PrintInfo(VideoInfo info)
        {
            if(info == null) return;

            var props = info.GetType().GetProperties();
            StringBuilder sb = new StringBuilder();
            foreach (PropertyInfo propertyInfo in props)
            {
                sb.AppendLine($"{propertyInfo.Name} : {propertyInfo.GetValue(info)}");
            }

            Console.WriteLine(sb.ToString());
        }
    }
}
