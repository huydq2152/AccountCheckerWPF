using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using AccountCheckerWPF.Enums;
using AccountCheckerWPF.Models;

namespace AccountCheckerWPF.Managers;

public class ProxyManager
{
    public List<Proxy> ProxyList { get; set; }
    public ProxyTypeEnums ProxyType { get; set; }
    public string ProxyAuthUser { get; set; }
    public string ProxyAuthPass { get; set; }

    private Random random = new Random();

    public Proxy GetRandomProxy()
    {
        if (ProxyList.Count == 0)
        {
            return null;
        }
        else if (ProxyList.Count == 1)
        {
            return ProxyList[0];
        }

        Proxy proxy;
        var rand = new Random();
        while (true)
        {
            proxy = ProxyList[rand.Next(ProxyList.Count)];
            if (!proxy.InUse && !proxy.Banned)
            {
                break;
            }

            if (GetLivingCount() == 0)
            {
                foreach (var p in ProxyList)
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
        return ProxyList.Count(p => !p.Banned);
    }

    public int LoadProxiesFromFile(string filename, ProxyTypeEnums proxyType)
    {
        ProxyType = proxyType;
        ProxyList = new List<Proxy>();

        using (var file = new StreamReader(filename))
        {
            string line;
            while ((line = file.ReadLine()) != null)
            {
                var parts = line.Split(':');
                if (parts.Length == 4)
                {
                    ProxyList.Add(new Proxy { Address = $"{parts[0]}:{parts[1]}", InUse = false });
                    ProxyAuthUser = parts[2];
                    ProxyAuthPass = parts[3];
                }
                else
                {
                    ProxyList.Add(new Proxy { Address = line, InUse = false });
                }
            }
        }

        return ProxyList.Count;
    }

    public HttpClientHandler GetRandomProxyTransport(out Proxy proxy)
    {
        proxy = GetRandomProxy();
        var handler = new HttpClientHandler
        {
            SslProtocols = SslProtocols.Tls11 | SslProtocols.Tls12,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
        };

        if (proxy == null)
        {
            return handler;
        }

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
        }

        if (!string.IsNullOrEmpty(ProxyAuthUser))
        {
            handler.Proxy.Credentials = new NetworkCredential(ProxyAuthUser, ProxyAuthPass);
            handler.PreAuthenticate = true;
        }

        return handler;
    }
}