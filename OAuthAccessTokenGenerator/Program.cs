using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;

namespace OAuthAccessTokenGenerator
{
    public static class Program
    {
        private static string CallbackUrl = "/oauth-cb";

        public static void Main(string[] args)
        {
            // two things it can do: get new access token, or refresh existing one
            // if a data.ini file exists, use that and refresh
            // if not exists, create new
            var data = OAuthData.TryReadData("data.ini");
            var t = data == null ? DoNew() : DoExisting(data);
            t.GetAwaiter().GetResult();
        }

        private static async Task DoNew()
        {
            var portStr = Prompt("Local free port (leave empty to pick at random)");
            var port = string.IsNullOrWhiteSpace(portStr) ? GetRandomFreePort() : int.Parse(portStr);
            var callbackUrl = GetCallbackUrlForPort(port);
            Console.WriteLine("Create a new OAuth application with the following callback/redirect url: " +
                              callbackUrl);
            var baseUrl = Prompt("OAuth Base Url");
            var clientId = Prompt("OAuth Client ID");
            var clientSecret = Prompt("OAuth Client Secret");
            var scopes = Prompt("OAuth scopes");
            var randomState = Guid.NewGuid().ToString();
            var authUrl = BuildAuthUrl(baseUrl, clientId, callbackUrl, scopes, randomState);
            var listener = new OAuthLocalhostCallbackListener(port, randomState, CallbackUrl);
            listener.Start();
            Console.WriteLine(
                "opening a browser window, follow steps there till it tells you to close the thing again and then come back here");
            Console.WriteLine("Waiting for valid response...");
            OpenBrowser(authUrl);
            var authCode = await listener.WaitForCode();
            Console.WriteLine("OAuth response received, now turning into an access token...");
            var token = await GetTokenFromAuthCode(baseUrl, clientId, clientSecret, callbackUrl, authCode);
            Console.WriteLine("Access token received, saving to disk...");

            var data = new OAuthData
            {
                OAuthBaseUrl = baseUrl,
                OAuthClientId = clientId,
                OAuthClientSecret = clientSecret,
                LocalPort = port,
                Scopes = scopes,
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken
            };
            await File.WriteAllLinesAsync("data.ini", data.ToLines());
            Console.WriteLine("Done!");
        }

        private static async Task DoExisting(OAuthData data)
        {
            Console.WriteLine("Refreshing oauth token...");
            var token = await GetTokenFromRefreshToken(data.OAuthBaseUrl, data.OAuthClientId, data.OAuthClientSecret,
                data.RefreshToken, data.Scopes);
            data.AccessToken = token.AccessToken;
            data.RefreshToken = token.RefreshToken;
            await File.WriteAllLinesAsync("data.ini", data.ToLines());
            Console.WriteLine("Done!");
        }

        private static Task<OAuthTokenResponse> GetTokenFromAuthCode(string baseUrl, string clientId,
            string clientSecret, string callbackUrl, string code) => HttpTokenRequest(baseUrl, "grant_type",
            "authorization_code", "client_id", clientId, "client_secret", clientSecret, "redirect_uri", callbackUrl,
            "code", code);

        private static Task<OAuthTokenResponse> GetTokenFromRefreshToken(string baseUrl, string clientId,
            string clientSecret, string refreshToken, string scope) => HttpTokenRequest(baseUrl, "grant_type",
            "refresh_token", "client_id", clientId, "client_secret", clientSecret, "refresh_token", refreshToken,
            "scope", scope);

        private static async Task<OAuthTokenResponse> HttpTokenRequest(string baseUrl, params string[] args)
        {
            var content = new FormUrlEncodedContent(Build(args));
            var url = BuildTokenUrl(baseUrl);
            using var client = new HttpClient();
            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<OAuthTokenResponse>(body);
            return token;
        }

        private static IEnumerable<KeyValuePair<string, string>> Build(params string[] args)
        {
            if ((args.Length % 2) != 0) throw new ArgumentException();
            for (var i = 0; i < args.Length; i += 2)
            {
                yield return new KeyValuePair<string, string>(args[i], args[i + 1]);
            }
        }

        public static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") {CreateNoWindow = true});
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        private static string BuildAuthUrl(string baseUrl, string clientId, string callbackUrl, string scopes,
            string state)
        {
            if (!baseUrl.EndsWith('/')) baseUrl += '/';
            return
                $"{baseUrl}authorize?client_id={WebUtility.UrlEncode(clientId)}&redirect_uri={WebUtility.UrlEncode(callbackUrl)}&response_type=code&scope={WebUtility.UrlEncode(scopes)}&state={WebUtility.UrlEncode(state)}";
        }

        private static string BuildTokenUrl(string baseUrl)
        {
            if (!baseUrl.EndsWith('/')) baseUrl += '/';
            return $"{baseUrl}token";
        }

        private static string GetCallbackUrlForPort(in int port) =>
            port == 80 ? "http://localhost/callback" : $"http://localhost:{port}{CallbackUrl}";

        private static string Prompt(string prompt)
        {
            Console.Write(prompt + ": ");
            return Console.ReadLine();
        }

        private static int GetRandomFreePort()
        {
            using var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint) sock.LocalEndPoint).Port;
        }
    }
}