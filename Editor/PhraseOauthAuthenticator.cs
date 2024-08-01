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

        private static readonly string listenUrl = "http://localhost:8000/oauth/";
        private static readonly string redirectUrl = $"{listenUrl}/callback";
        private static string pageSuccess =
@"<html>
  <head>
    <title>Authorization successful</title>
  </head>
  <body>
    <p>You may close this page now</p>
  </body>
</html>";

        private static readonly string clientId = "strings-unity";

        private static string BaseUrl
        {
            get {
                switch (provider.m_Environment)
                {
                    case "EU":
                        return "https://eu.phrase.com";
                    case "US":
                        return "https://us.phrase.com";
                    default:
                        return "https://eu.phrase-qa.com";
                };
            }
        }

        private static string codeVerifier = "";

        private static readonly string challengeMethod = "S256";

        private static string accessToken = null;

        private static string organizationId = null;

        private static void HandleAuthorizationCodeFlow()
        {
            codeVerifier = GenerateRandomString(64);
            var codeChallenge = GenerateCodeChallenge(codeVerifier);

            Application.OpenURL($"{BaseUrl}/idm/openid/authorize?response_type=code&scope=openid%20sso&client_id={clientId}&code_challenge_method={challengeMethod}&code_challenge={codeChallenge}&redirect_uri={redirectUrl}&login_hint=__idm__");
        }

        private static async Task<PhraseAuthTokenResponse> GetAccessToken(string code)
        {
            provider.Log($"Getting access token from code {code}");
            var httpClient = new HttpClient();
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("code_verifier", codeVerifier),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", redirectUrl),
                new KeyValuePair<string, string>("code", code)
            });

            var response = await httpClient.PostAsync($"{BaseUrl}/idm/oauth/token", content);
            var jsonResponse =  await response.Content.ReadAsStringAsync();
            provider.Log($"Response: {jsonResponse}");
            return JsonUtility.FromJson<PhraseAuthTokenResponse>(jsonResponse);
        }

        private static async Task<PhraseUserResponse> GetUser(string token)
        {
            provider.Log($"Getting user from token {token}");
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var response = await httpClient.GetAsync($"{BaseUrl}/idm/api/v1/user");
            var jsonResponse = await response.Content.ReadAsStringAsync();
            provider.Log($"Response: {jsonResponse}");
            return JsonUtility.FromJson<PhraseUserResponse>(jsonResponse);
        }

        private static async Task<PhraseAppTokenResponse> GetAppToken(string token, string organizationId)
        {
            provider.Log($"Getting app token from token {token} and organization {organizationId}");
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var body = new StringContent("{\"applicationUid\":\"strings\"}", Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"{BaseUrl}/idm/api/v1/user/organizations/{organizationId}/token/grant", body);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            provider.Log($"Response: {jsonResponse}");
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
                provider.Log("Waiting for a request...");
                // Will wait here until we hear from a connection
                HttpListenerContext ctx = await listener.GetContextAsync();
                HttpListenerRequest req = ctx.Request;

                // Print out some info about the request
                provider.Log($"Request: {req.HttpMethod} {req.Url.ToString()}");

                string code = Regex.Match(req.Url.Query, "code=([^&]*)").Groups[1].Value;
                if (code != "")
                {
                    var accessTokenResponse = await GetAccessToken(code);
                    accessToken = accessTokenResponse.access_token;
                    var userResponse = await GetUser(accessToken);
                    organizationId = userResponse.lastOrganization.uid;
                    await RefreshToken();
                    SendPageContent(ctx.Response, pageSuccess);
                    runServer = false;
                    provider.Log("Closing server...");
                    StopServer();
                } else {
                    SendPageContent(ctx.Response, "Error: No code found in the request");
                }
            }
        }

        public static async Task<bool> RefreshToken()
        {
            var appTokenResponse = await GetAppToken(accessToken, organizationId);
            provider.Log($"Token: {appTokenResponse.accessToken}");
            provider.SetOauthToken(appTokenResponse.accessToken);
            return true;
        }

        private static void SendPageContent(HttpListenerResponse response, string content)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }

        private static void StartServer()
        {
            // Create a Http server and start listening for incoming connections
            listener = new HttpListener();
            listener.Prefixes.Add(listenUrl);
            listener.Start();
            provider.Log($"Listening for connections on {listenUrl}");

            // Handle requests
            Task listenTask = HandleIncomingConnections();
        }

        private static void StopServer()
        {
            listener.Close();
        }

        public static void Authenticate(PhraseProvider provider) {
            PhraseOauthAuthenticator.provider = provider;
            StartServer();
            HandleAuthorizationCodeFlow();
        }
    }
}
