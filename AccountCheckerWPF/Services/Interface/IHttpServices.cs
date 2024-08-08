using System.Net.Http;

namespace AccountCheckerWPF.Services.Interface;

public interface IHttpServices
{
    void InitHttpClient();
    public Task<HttpResponseMessage> SendGetRequestAsync();
    public Task<HttpResponseMessage> SendPostRequestAsync(string email, string password, string ppft, string contextid,
        string bk, string uaid);
    public Task<HttpResponseMessage> SendPostRequestToGetAccessTokenAsync(string refreshToken);
}