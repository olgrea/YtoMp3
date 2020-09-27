using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using YtoMp3.Utils;

namespace YtoMp3
{
    internal class YoutubeClient
    {
        private YoutubeHttpClient m_Client;

        public YoutubeClient()
        {
            m_Client = new YoutubeHttpClient();
        }

        public async Task<VideoInfo> GetVideoInfo(string url)
        {
            string id = Url.ParseQuery(url)["v"];
            
            var str = await m_Client.GetStringAsync(InfoUrl + id);
            var parsed = Url.ParseQuery(str);
            var response = HttpUtility.UrlDecode(parsed["player_response"]);

            JsonDocument doc = JsonDocument.Parse(response);
            var elem = doc.RootElement.Clone();
            
            return VideoInfo.Parse(elem);
        }


        private const string InfoUrl = "https://www.youtube.com/get_video_info?video_id=";

    }
}
