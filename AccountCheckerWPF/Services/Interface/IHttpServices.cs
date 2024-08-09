using System.Net.Http;
using AccountCheckerWPF.Models;

namespace AccountCheckerWPF.Services.Interface;

public interface IHttpServices
{
    public Task<HttpResponseMessage> SendGetParamsFromLoginPageRequestAsync();
    public Task<HttpResponseMessage> SendPostLoginRequestAsync(string email, string password, string ppft, string contextid,
        string bk, string uaid);
    public Task HandleLoginSuccessResponse(HttpResponseMessage postResponse, List<string> cookies, Account account);
}