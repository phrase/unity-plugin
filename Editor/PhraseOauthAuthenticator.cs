using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using UnityEngine;

namespace Phrase
{
    [Serializable]
    struct PhraseAuthTokenResponse
    {
        public string access_token;
        public string token_type;
        public string expires_in;
        public string scope;
        public string refresh_token;
    }

    [Serializable]
    struct PhraseOrganization {
        public string uid;
    }

    [Serializable]
    struct PhraseUserResponse
    {
        public string uid;
        public string email;
        public string username;
        public PhraseOrganization lastOrganization;
    }

    [Serializable]
    struct PhraseAppTokenResponse
    {
        public string accessToken;
    }

    class PhraseOauthAuthenticator
    {
        private static PhraseProvider provider;

        private static HttpListener listener;
        private static readonly string redirectUrl = "http://localhost:8000/";
        private static string pageData =
            "<!DOCTYPE>" +
            "<html>" +
            "  <head>" +
            "    <title>Authorization successful</title>" +
            "  </head>" +
            "  <body>" +
            "    <p>You may close this page now</p>" +
            "  </body>" +
            "</html>";

        private static readonly string clientId = "strings-unity";
        // private static readonly string baseUrl = "https://eu.phrase.com";

        private static readonly string baseUrl = "https://eu.phrase-qa.com";

        private static string codeVerifier = "";

        private static readonly string challengeMethod = "S256";

        private static void HandleAuthorizationCodeFlow()
        {
            codeVerifier = GenerateRandomString(64);
            var codeChallenge = GenerateCodeChallenge(codeVerifier);

            Application.OpenURL($"{baseUrl}/idm/openid/authorize?response_type=code&scope=openid%20sso&client_id={clientId}&code_challenge_method={challengeMethod}&code_challenge={codeChallenge}&redirect_uri={redirectUrl}&login_hint=__idm__");
        }

        private static async Task<PhraseAuthTokenResponse> GetAccessToken(string code)
        {
            Debug.Log($"Getting access token from code {code}");
            var httpClient = new HttpClient();
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("code_verifier", codeVerifier),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", redirectUrl),
                new KeyValuePair<string, string>("code", code)
            });

            var response = await httpClient.PostAsync($"{baseUrl}/idm/oauth/token", content);
            var jsonResponse =  await response.Content.ReadAsStringAsync();
            Debug.Log($"Response: {jsonResponse}");
            return JsonUtility.FromJson<PhraseAuthTokenResponse>(jsonResponse);
        }

        private static async Task<PhraseUserResponse> GetUser(string token)
        {
            Debug.Log($"Getting user from token {token}");
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var response = await httpClient.GetAsync($"{baseUrl}/idm/api/v1/user");
            var jsonResponse = await response.Content.ReadAsStringAsync();
            Debug.Log($"Response: {jsonResponse}");
            return JsonUtility.FromJson<PhraseUserResponse>(jsonResponse);
        }

        private static async Task<PhraseAppTokenResponse> GetAppToken(string token, string organizationId)
        {
            Debug.Log($"Getting app token from token {token} and organization {organizationId}");
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var body = new StringContent("{\"applicationUid\":\"strings\"}", Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"{baseUrl}/idm/api/v1/user/organizations/{organizationId}/token/grant", body);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            Debug.Log($"Response: {jsonResponse}");
            return JsonUtility.FromJson<PhraseAppTokenResponse>(jsonResponse);
        }

        private static string GenerateCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
                return Convert.ToBase64String(bytes)
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            }
        }

        private static string GenerateRandomString(int length)
        {
            var text = new StringBuilder();
            const string possible = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

            var random = new System.Random();
            for (var i = 0; i < length; i++)
            {
                text.Append(possible[random.Next(possible.Length)]);
            }

            return text.ToString();
        }

        private static async Task HandleIncomingConnections()
        {
            bool runServer = true;

            // While a user hasn't successfully authenticated, keep on handling requests
            while (runServer)
            {
                Debug.Log("Waiting for a request...");
                // Will wait here until we hear from a connection
                HttpListenerContext ctx = await listener.GetContextAsync();

                // Peel out the requests and response objects
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                // Print out some info about the request
                Debug.Log($"Request: {req.HttpMethod} {req.Url.ToString()}");

                string code = Regex.Match(req.Url.Query, "code=([^&]*)").Groups[1].Value;
                if (code != "")
                {
                    var accessToken = await GetAccessToken(code);
                    var user = await GetUser(accessToken.access_token);
                    var appToken = await GetAppToken(accessToken.access_token, user.lastOrganization.uid);

                    Debug.Log($"Token: {appToken.accessToken}");
                    provider.m_ApiKey = appToken.accessToken;
                    runServer = false;
                    Debug.Log("Closing server...");
                }

                // Write the response info
                byte[] data = Encoding.UTF8.GetBytes(pageData);
                resp.ContentType = "text/html";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;

                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();
            }
        }

        private static void StartServer()
        {
            // Create a Http server and start listening for incoming connections
            listener = new HttpListener();
            listener.Prefixes.Add(redirectUrl);
            listener.Start();
            Debug.Log($"Listening for connections on {redirectUrl}");

            // Handle requests
            Task listenTask = HandleIncomingConnections();
            // listenTask.GetAwaiter().GetResult();
        }

        private static void StopServer()
        {
            // Close the listener
            listener.Close();
        }

        public static void Authenticate(PhraseProvider provider) {
            PhraseOauthAuthenticator.provider = provider;
            StartServer();
            HandleAuthorizationCodeFlow();
        }
    }
}
