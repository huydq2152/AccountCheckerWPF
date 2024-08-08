using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using AccountCheckerWPF.Enums;
using AccountCheckerWPF.Models;

namespace AccountCheckerWPF.Managers;

public class ProxyManager
{
    public List<Proxy> Proxies { get; set; }
    public ProxyTypeEnums ProxyType { get; set; }
    public string AuthUser { get; set; }
    public string AuthPass { get; set; }

    public Proxy GetRandomProxy()
    {
        if (Proxies.Count == 0)
        {
            return null;
        }
        else if (Proxies.Count == 1)
        {
            return Proxies[0];
        }

        Proxy proxy;
        var rand = new Random();
        while (true)
        {
            proxy = Proxies[rand.Next(Proxies.Count)];
            if (!proxy.InUse && !proxy.Banned)
            {
                break;
            }

            if (GetLivingCount() == 0)
            {
                foreach (var p in Proxies)
                {
                    p.Banned = false;
                }
            }
        }

        proxy.InUse = true;
        return proxy;
    }

    public int GetLivingCount()
    {
        return Proxies.Count(p => !p.Banned);
    }

    public int LoadProxiesFromFile(string filename, ProxyTypeEnums proxyType)
    {
        ProxyType = proxyType;
        Proxies = new List<Proxy>();

        using (var file = new StreamReader(filename))
        {
            string line;
            while ((line = file.ReadLine()) != null)
            {
                var parts = line.Split(':');
                if (parts.Length == 4)
                {
                    Proxies.Add(new Proxy { Address = $"{parts[0]}:{parts[1]}", InUse = false });
                    AuthUser = parts[2];
                    AuthPass = parts[3];
                }
                else
                {
                    Proxies.Add(new Proxy { Address = line, InUse = false });
                }
            }
        }

        return Proxies.Count;
    }

    public HttpClientHandler GetRandomProxyTransport(out Proxy proxy)
    {
        proxy = GetRandomProxy();
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
        };

        switch (ProxyType)
        {
            case ProxyTypeEnums.HTTP:
                handler.Proxy = new WebProxy($"http://{proxy.Address}");
                break;
            case ProxyTypeEnums.SOCKS4:
                handler.Proxy = new WebProxy($"socks4://{proxy.Address}");
                break;
            case ProxyTypeEnums.SOCKS4A:
                handler.Proxy = new WebProxy($"socks4a://{proxy.Address}");
                break;
            case ProxyTypeEnums.SOCKS5:
                handler.Proxy = new WebProxy($"socks5://{proxy.Address}");
                break;
            default:
                handler.Proxy = new WebProxy($"http://{proxy.Address}");
                break;
        }

        if (!string.IsNullOrEmpty(AuthUser))
        {
            handler.Proxy.Credentials = new NetworkCredential(AuthUser, AuthPass);
            handler.PreAuthenticate = true;
        }

        return handler;
    }
}