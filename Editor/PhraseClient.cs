
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

namespace Phrase
{
    public class Client
    {
        private readonly string accessToken;

        private readonly string apiUrl = "https://api.phrase.com/v2";

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

        public Client(string accessToken)
        {
            this.accessToken = accessToken;
        }

        public Client(string accessToken, string apiUrl)
        {
            this.accessToken = accessToken;
            this.apiUrl = apiUrl;
        }

        private HttpWebRequest createRequest(string url) {
            Debug.Log("Creating request to " + url);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            NetworkCredential myNetworkCredential = new NetworkCredential(accessToken, "");
            Uri uri = new Uri(url);
            request.PreAuthenticate = true;
            CredentialCache myCredentialCache = new CredentialCache();
            myCredentialCache.Add(uri, "Basic", myNetworkCredential);
            request.Credentials = myCredentialCache;
            request.UserAgent = "Unity Plugin/1.0";
            return request;
        }

        public string DownloadLocale(string projectID, string localeID)
        {
            string url = string.Format("{0}/projects/{1}/locales/{2}/download?file_format=xlf&include_empty_translations=true", apiUrl, projectID, localeID);
            HttpWebRequest request = createRequest(url);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            StreamReader reader = new StreamReader(response.GetResponseStream());
            string content = reader.ReadToEnd();
            return content;
        }


        public List<Locale> ListLocales(string projectID)
        {
            string url = string.Format("{0}/projects/{1}/locales", apiUrl, projectID);
            HttpWebRequest request = createRequest(url);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            StreamReader reader = new StreamReader(response.GetResponseStream());
            string jsonResponse = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<List<Locale>>(jsonResponse);
        }

        public List<Project> ListProjects()
        {
            string url = string.Format("{0}/projects?per_page=100", apiUrl);
            HttpWebRequest request = createRequest(url);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            StreamReader reader = new StreamReader(response.GetResponseStream());
            string jsonResponse = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<List<Project>>(jsonResponse);
        }

        public void UploadFile(string translations, string projectID, string localeID, bool autoTranslate)
        {
            string url = string.Format("{0}/projects/{1}/uploads", apiUrl, projectID);
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
            string url = string.Format("{0}/projects/{1}/screenshots", apiUrl, projectID);
            NameValueCollection nvc = new NameValueCollection();
            var response = HttpUploadFile(url, "unity.jpg", "filename", "image/jpeg", nvc, image);
            var screenshot = JsonUtility.FromJson<Screenshot>(response);
            return screenshot;
        }

        string HttpUploadFile(string url, string file, string paramName, string contentType, NameValueCollection nvc, byte[] content)
        {
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            HttpWebRequest wr = createRequest(url);
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
                Debug.Log(string.Format("File uploaded, server response is: {0}", response));
                return response;
            }
            catch (WebException ex)
            {
                using (var stream = ex?.Response?.GetResponseStream())
                    if (stream != null)
                        using (var reader = new StreamReader(stream))
                        {
                            Debug.Log(reader.ReadToEnd());
                        }

                Debug.LogError("Error uploading file" + ex.ToString());
                if (wresp != null)
                {
                    Debug.Log("not null");
                    using (var reader = new StreamReader(wresp.GetResponseStream()))
                    {
                        string result = reader.ReadToEnd(); // do something fun...
                        Debug.Log("test");
                        Debug.Log(result);
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