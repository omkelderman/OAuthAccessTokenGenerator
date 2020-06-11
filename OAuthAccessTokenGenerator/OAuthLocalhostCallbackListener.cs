using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace OAuthAccessTokenGenerator
{
    public class OAuthLocalhostCallbackListener
    {
        private readonly string _state;
        private readonly string _callbackUrl;
        private readonly HttpListener _httpListener;

        public OAuthLocalhostCallbackListener(int port, string state, string callbackUrl)
        {
            _state = state;
            _callbackUrl = callbackUrl;
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{port}/");
        }

        public void Start()
        {
            _httpListener.Start();
        }

        public async Task<string> WaitForCode()
        {
            for (;;)
            {
                var context = await _httpListener.GetContextAsync();
                if (context.Request.HttpMethod != "GET" || context.Request.Url.LocalPath != _callbackUrl)
                {
                    await SendResponse(context.Response, 404, "NOPE");
                    continue;
                }

                // url is correct, lets check if state is correct
                var q = context.Request.QueryString;
                var state = q.GetValues("state")?.FirstOrDefault();
                var code = q.GetValues("code")?.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code))
                {
                    await SendResponse(context.Response, 404, "missing query args");
                    continue;
                }

                if (state != _state)
                {
                    await SendResponse(context.Response, 404, "wrong state");
                    continue;
                }

                await SendResponse(context.Response, 200, "Success! You can close this page now");
                return code;
            }
        }

        private static async Task SendResponse(HttpListenerResponse response, int statusCode, string bodyText)
        {
            var body = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(bodyText));
            response.StatusCode = statusCode;
            await response.OutputStream.WriteAsync(body);
            response.OutputStream.Close();
        }
    }
}