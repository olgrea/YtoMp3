using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Web;

namespace YtoMp3
{
    public class VideoInfo
    {
        public string Id { get; internal set; }
        public string Url => $"https://www.youtube.com/watch?v={Id}";
        public string Title { get; internal set; }
        public string ChannelId { get; internal set; }
        public TimeSpan Duration { get; internal set; }
        
        public static VideoInfo Parse(JsonElement element)
        {
            var details = element.GetProperty("videoDetails");

            return new VideoInfo()
            {
                Id = details.GetProperty("videoId").ToString(),
                Title = details.GetProperty("title").ToString(),
                ChannelId = details.GetProperty("channelId").ToString(),
                Duration = TimeSpan.FromSeconds(int.Parse(details.GetProperty("lengthSeconds").ToString()))
            };
        }
    }
}
