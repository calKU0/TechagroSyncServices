using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace IntercarsSyncService.Helpers
{
    public static class HttpClientHelper
    {
        public static HttpClient CreateAuthorizedClient(string username, string password, bool allowAutoRedirect = false)
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = allowAutoRedirect
            };

            var client = new HttpClient(handler);

            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

            return client;
        }
    }
}