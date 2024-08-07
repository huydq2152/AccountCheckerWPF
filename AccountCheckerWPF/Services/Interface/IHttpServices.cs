using System.Net.Http;

namespace AccountCheckerWPF.Services.Interface;

public interface IHttpServices
{
    public Task<HttpResponseMessage> SendGetRequestAsync();

    public Task<HttpResponseMessage> SendPostRequestAsync(string email, string password, string ppft, string contextid,
        string bk, string uaid);

    public Task<HttpResponseMessage> SendPostRequestToGetAccessTokenAsync(string refreshToken);
}