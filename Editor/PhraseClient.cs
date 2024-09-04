using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Phrase
{
    public class RetryHandler : DelegatingHandler
    {
        private const int MaxRetries = 1;

        private readonly PhraseProvider Provider;

        public RetryHandler(HttpMessageHandler innerHandler, PhraseProvider provider) : base(innerHandler)
        {
            Provider = provider;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            request.Headers.Add("Authorization", "Bearer " + Provider.Token);
            for (int i = 0; i < MaxRetries; i++)
            {
                Provider.Log("Sending request to " + request.RequestUri);
                response = base.SendAsync(request, cancellationToken).Result;
                if (response.IsSuccessStatusCode) {
                    return response;
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Provider.Log("Unauthorized, refreshing token");
                    if (await Provider.RefreshToken()) {
                        request.Headers.Remove("Authorization");
                        request.Headers.Add("Authorization", "Bearer " + Provider.Token);
                    } else {
                        Provider.Log("Request unauthorized, failed to refresh token");
                        return response;
                    }
                }
                else
                {
                    Provider.Log("Request failed with status code " + response.StatusCode);
                    return response;
                }
            }

            return response;
        }
    }

    public class PhraseClient
    {
        private readonly PhraseProvider Provider;

        private string ApiUrl => Provider.m_ApiUrl;

        private string AccessToken => Provider.Token;

        private HttpClient Client;

        [Serializable]
        public class Locale
        {
            public string id;
            public string name;
            public string code;

            public override string ToString()
            {
                return name == code ? name : $"{name} ({code})";
            }
        }

        [Serializable]
        public class Project
        {
            public string id;
            public string name;
        }

        [Serializable]
        public class Screenshot
        {
            public string id;
        }

        [Serializable]
        public class Key
        {
            public string id;
            public string name;
        }

        public PhraseClient(PhraseProvider provider)
        {
            this.Provider = provider;
            this.Client = new HttpClient(new RetryHandler(new HttpClientHandler(), provider));
            // Client.DefaultRequestHeaders.Add("Authorization", "Bearer " + AccessToken);
            Client.DefaultRequestHeaders.Add("User-Agent", "Unity Plugin/1.0");
            Client.BaseAddress = new Uri(ApiUrl);
        }

        public async Task<string> DownloadLocale(string projectID, string localeID)
        {
            string url = string.Format("projects/{0}/locales/{1}/download?file_format=csv&format_options%5Bexport_max_characters_allowed%5D=true&include_empty_translations=true", projectID, localeID);
            using HttpResponseMessage response = await Client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }


        public async Task<List<Locale>> ListLocales(string projectID)
        {
            string url = string.Format("projects/{0}/locales", projectID);
            using HttpResponseMessage response = await Client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<Locale>>(jsonResponse);
        }

        public async void CreateLocale(string projectID, string localeCode, string localeName)
        {
            string url = string.Format("projects/{0}/locales", projectID);
            var content = new StringContent(JsonConvert.SerializeObject(new { code = localeCode, name = localeName }), Encoding.UTF8, "application/json");
            var response = await Client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<Project>> ListProjects()
        {
            using HttpResponseMessage response = await Client.GetAsync("projects?per_page=100");
            response.EnsureSuccessStatusCode();
            string jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<Project>>(jsonResponse);
        }

        public async void UploadFile(string path, string projectID, string localeID, string localeCode, bool autoTranslate)
        {
            string url = string.Format("projects/{0}/uploads", projectID);
            NameValueCollection nvc = new NameValueCollection
            {
                { "locale_id", localeID },
                { "file_format", "csv" },
                { "header_content_row", "true" },
                { "update_descriptions", "true" },
                { "autotranslate", autoTranslate.ToString() },
                { $"locale_mapping[{localeCode}]", "2" },
                { "format_options[key_index]", "1" },
                { "format_options[comment_index]", "3" },
                { "format_options[max_characters_allowed_column]", "4" }
            };

            await HttpUploadFile(url, path, "file", "text/plain", nvc);
        }

        public async Task<Screenshot> UploadScreenshot(string projectID, string name, string path)
        {
            string url = string.Format("projects/{0}/screenshots", projectID);
            NameValueCollection nvc = new NameValueCollection
            {
                { "name", name },
                { "description", "Uploaded from Unity" }
            };
            string responseString = await HttpUploadFile(url, path, "filename", "image/png", nvc);
            return JsonConvert.DeserializeObject<Screenshot>(responseString);
        }

        public void CreateScreenshotMarker(string projectID, string screenshotID, string keyID)
        {
            string url = string.Format("projects/{0}/screenshots/{1}/markers", projectID, screenshotID);
            var content = new StringContent(JsonConvert.SerializeObject(new { key_id = keyID }), Encoding.UTF8, "application/json");
            var response = Client.PostAsync(url, content).Result;
            response.EnsureSuccessStatusCode();
        }

        public async Task<Key> GetKey(string projectID, string keyName)
        {
            string url = string.Format($"projects/{{0}}/keys?q=name:{Uri.EscapeDataString(keyName)}", projectID);
            using HttpResponseMessage response = await Client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string jsonResponse = await response.Content.ReadAsStringAsync();
            List<Key> keys = JsonConvert.DeserializeObject<List<Key>>(jsonResponse);
            return keys.Find(k => k.name == keyName);
        }

        private async Task<string> HttpUploadFile(string url, string path, string paramName, string contentType, NameValueCollection nvc)
        {
            MultipartFormDataContent form = new MultipartFormDataContent();
            var fileStream = new FileStream(path, FileMode.Open);
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.Add("Content-Type", contentType);
            var fileName = Path.GetFileName(path);
            form.Add(streamContent, paramName, fileName);

            foreach (string key in nvc.Keys)
            {
                form.Add(new StringContent(nvc[key]), key);
            }

            var response = await Client.PostAsync(url, form);
            var responseContent = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            return Task.FromResult(responseContent).Result;
        }
    }
}