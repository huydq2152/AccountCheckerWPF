using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using AccountCheckerWPF.Enums;
using AccountCheckerWPF.Models;

namespace AccountCheckerWPF.Managers;

public class ProxyManager
{
    private List<Proxy?>? Proxies { get; set; }
    private ProxyTypeEnums ProxyType { get; set; }
    private string? AuthUser { get; set; }
    private string? AuthPass { get; set; }

    private Proxy? GetRandomProxy()
    {
        switch (Proxies?.Count)
        {
            case 0:
                return null;
            case 1:
                return Proxies[0];
        }

        Proxy? proxy;
        var rand = new Random();
        while (true)
        {
            proxy = Proxies?[rand.Next(Proxies.Count)];
            if (proxy is { InUse: false, Banned: false })
            {
                break;
            }

            if (GetLivingCount() == 0 && Proxies != null)
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

    private int GetLivingCount()
    {
        return Proxies?.Count(p => p is { Banned: false }) ?? 0;
    }

    public void LoadProxiesFromFile(string filename, ProxyTypeEnums proxyType)
    {
        ProxyType = proxyType;
        Proxies = new List<Proxy?>();

        using var file = new StreamReader(filename);
        while (file.ReadLine() is { } line)
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

    public HttpClientHandler GetRandomProxyTransport(out Proxy? proxy)
    {
        proxy = GetRandomProxy();
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
        };

        handler.Proxy = ProxyType switch
        {
            ProxyTypeEnums.HTTP => new WebProxy($"http://{proxy?.Address}"),
            ProxyTypeEnums.SOCKS4 => new WebProxy($"socks4://{proxy?.Address}"),
            ProxyTypeEnums.SOCKS4A => new WebProxy($"socks4a://{proxy?.Address}"),
            ProxyTypeEnums.SOCKS5 => new WebProxy($"socks5://{proxy?.Address}"),
            _ => new WebProxy($"http://{proxy?.Address}")
        };

        if (string.IsNullOrEmpty(AuthUser)) return handler;
        handler.Proxy.Credentials = new NetworkCredential(AuthUser, AuthPass);
        handler.PreAuthenticate = true;

        return handler;
    }
}