using System.Net.Http;

namespace AccountCheckerWPF.Services.Interface;

public interface IHttpServices
{
    void InitHttpClient();
    public Task<HttpResponseMessage> SendGetParamsFromLoginPageRequestAsync();
    public Task<HttpResponseMessage> SendPostLoginRequestAsync(string email, string password, string ppft, string contextid,
        string bk, string uaid);
    public Task<HttpResponseMessage> SendPostRequestToGetAccessTokenAsync(string refreshToken);
}