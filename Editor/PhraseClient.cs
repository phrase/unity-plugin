
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
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
                response = await base.SendAsync(request, cancellationToken);
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

        public PhraseClient(PhraseProvider provider)
        {
            this.Provider = provider;
            this.Client = new HttpClient(new RetryHandler(new HttpClientHandler(), provider));
            // Client.DefaultRequestHeaders.Add("Authorization", "Bearer " + AccessToken);
            Client.DefaultRequestHeaders.Add("User-Agent", "Unity Plugin/1.0");
            Client.BaseAddress = new Uri(ApiUrl);
        }

        private HttpWebRequest CreateRequest(string url) {
            Provider.Log("Creating request to " + url);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            Uri uri = new Uri(url);
            request.PreAuthenticate = true;

            // authenticate using the access token
            request.Headers.Add("Authorization", "Bearer " + AccessToken);

            request.UserAgent = "Unity Plugin/1.0";
            return request;
        }

        public async Task<string> DownloadLocale(string projectID, string localeID)
        {
            string url = string.Format("projects/{0}/locales/{1}/download?file_format=xlf&include_empty_translations=true", projectID, localeID);
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

        public async Task<List<Project>> ListProjects()
        {
            using HttpResponseMessage response = await Client.GetAsync("projects?per_page=100");
            response.EnsureSuccessStatusCode();
            string jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<Project>>(jsonResponse);
        }

        public void UploadFile(string translations, string projectID, string localeID, bool autoTranslate)
        {
            string url = string.Format("{0}/projects/{1}/uploads", ApiUrl, projectID);
            NameValueCollection nvc = new NameValueCollection();
            nvc.Add("locale_id", localeID);
            nvc.Add("file_format", "xlf");
            nvc.Add("autotranslate", autoTranslate.ToString());
            nvc.Add("format_options[key_name_attribute]", "resname");

            byte[] content = Encoding.ASCII.GetBytes(translations);

            HttpUploadFile(url, "translations.xlf", "file", "text/plain", nvc, content);
        }

        public Screenshot UploadScreenshot(byte[] image, string projectID, string localeID)
        {
            string url = string.Format("{0}/projects/{1}/screenshots", ApiUrl, projectID);
            NameValueCollection nvc = new NameValueCollection();
            var response = HttpUploadFile(url, "unity.jpg", "filename", "image/jpeg", nvc, image);
            var screenshot = JsonUtility.FromJson<Screenshot>(response);
            return screenshot;
        }

        string HttpUploadFile(string url, string file, string paramName, string contentType, NameValueCollection nvc, byte[] content)
        {
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            HttpWebRequest wr = CreateRequest(url);
            wr.ContentType = "multipart/form-data; boundary=" + boundary;
            wr.Method = "POST";
            wr.KeepAlive = true;

            Stream rs = wr.GetRequestStream();

            string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
            foreach (string key in nvc.Keys)
            {
                rs.Write(boundarybytes, 0, boundarybytes.Length);
                string formitem = string.Format(formdataTemplate, key.Replace(@"\", @"\\").Replace(@"""", @"\"""), nvc[key]);
                byte[] formitembytes = Encoding.UTF8.GetBytes(formitem);
                rs.Write(formitembytes, 0, formitembytes.Length);
            }
            rs.Write(boundarybytes, 0, boundarybytes.Length);

            string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
            string header = string.Format(headerTemplate, paramName, file, contentType);
            byte[] headerbytes = Encoding.UTF8.GetBytes(header);
            rs.Write(headerbytes, 0, headerbytes.Length);
            rs.Write(content, 0, content.Length);

            byte[] trailer = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
            rs.Write(trailer, 0, trailer.Length);
            rs.Close();

            WebResponse wresp = null;
            try
            {
                wresp = wr.GetResponse();
                Stream stream2 = wresp.GetResponseStream();
                StreamReader reader2 = new StreamReader(stream2);
                var response = reader2.ReadToEnd();
                Provider.Log(string.Format("File uploaded, server response is: {0}", response));
                return response;
            }
            catch (WebException ex)
            {
                using (var stream = ex?.Response?.GetResponseStream())
                    if (stream != null)
                        using (var reader = new StreamReader(stream))
                        {
                            Provider.Log(reader.ReadToEnd());
                        }

                Provider.LogError("Error uploading file" + ex.ToString());
                if (wresp != null)
                {
                    Provider.Log("not null");
                    using (var reader = new StreamReader(wresp.GetResponseStream()))
                    {
                        string result = reader.ReadToEnd(); // do something fun...
                        Provider.Log("test");
                        Provider.Log(result);
                    }
                    wresp.Close();
                    wresp = null;
                }
            }
            finally
            {
                wr = null;
            }

            return null;
        }
    }
}