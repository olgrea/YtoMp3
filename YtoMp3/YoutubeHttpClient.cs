using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace YtoMp3
{
    public class YoutubeHttpClient
    {
        private readonly HttpClient m_HttpClient;

        public YoutubeHttpClient()
        {
            m_HttpClient = new HttpClient();
        }

        public async Task<HttpResponseMessage> GetAsync(string uri)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            return await m_HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        }

        public async Task<string> GetStringAsync(string uri)
        {
            using var response = await GetAsync(uri);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
