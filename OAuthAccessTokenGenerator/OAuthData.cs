using System.Collections.Generic;
using System.IO;

namespace OAuthAccessTokenGenerator
{
    public class OAuthData
    {
        public string OAuthBaseUrl { get; set; }
        public string OAuthClientId { get; set; }
        public string OAuthClientSecret { get; set; }
        public int LocalPort { get; set; }
        public string Scopes { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }

        public static OAuthData TryReadData(string filename)
        {
            string[] fileData;
            try
            {
                fileData = File.ReadAllLines(filename);
            }
            catch (FileNotFoundException)
            {
                return null;
            }

            return Parse(fileData);
        }

        public static OAuthData Parse(string[] lines)
        {
            var data = new OAuthData();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.StartsWith("//")) continue;
                var tmp = line.Split('=', 2);
                if (tmp.Length != 2) continue;
                var key = tmp[0].Trim();
                var val = tmp[1].Trim();
                switch (key)
                {
                    case nameof(OAuthBaseUrl):
                        data.OAuthBaseUrl = val;
                        break;
                    case nameof(OAuthClientId):
                        data.OAuthClientId = val;
                        break;
                    case nameof(OAuthClientSecret):
                        data.OAuthClientSecret = val;
                        break;
                    case nameof(LocalPort):
                        data.LocalPort = int.Parse(val);
                        break;
                    case nameof(Scopes):
                        data.Scopes = val;
                        break;
                    case nameof(AccessToken):
                        data.AccessToken = val;
                        break;
                    case nameof(RefreshToken):
                        data.RefreshToken = val;
                        break;
                }
            }

            return data;
        }

        public IEnumerable<string> ToLines()
        {
            yield return $"{nameof(OAuthBaseUrl)}={OAuthBaseUrl}";
            yield return $"{nameof(OAuthClientId)}={OAuthClientId}";
            yield return $"{nameof(OAuthClientSecret)}={OAuthClientSecret}";
            yield return $"{nameof(LocalPort)}={LocalPort}";
            yield return $"{nameof(Scopes)}={Scopes}";
            yield return $"{nameof(AccessToken)}={AccessToken}";
            yield return $"{nameof(RefreshToken)}={RefreshToken}";
        }
    }
}