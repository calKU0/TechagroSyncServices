using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace IntercarsSyncService.Helpers
{
    public static class HttpClientHelper
    {
        public static HttpClient CreateAuthorizedClient(string username, string password)
        {
            var client = new HttpClient();
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
            return client;
        }
    }
}
