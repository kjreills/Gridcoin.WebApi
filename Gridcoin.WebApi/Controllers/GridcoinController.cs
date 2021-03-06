using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Gridcoin.WebApi.Constants;
using Gridcoin.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gridcoin.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class GridcoinController : ControllerBase
    {
        public const string HttpClientKey = "gridcoin";

        private readonly ILogger<GridcoinController> _logger;
        private readonly IHttpClientFactory _http;
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        public GridcoinController(ILogger<GridcoinController> logger, IHttpClientFactory http)
        {
            _logger = logger;
            _http = http;
        }

        [HttpGet("getInfo")]
        [Authorize(Policy = Permissions.ReadInfo)]
        public Task<object> GetInfo()
        {
            return MakeRpcRequest(new RpcRequest(nameof(GetInfo)));
        }

        [HttpGet("getTransaction/{transactionId}")]
        [Authorize(Policy = Permissions.ReadInfo)]
        public Task<object> GetTransaction(string transactionId)
        {
            return MakeRpcRequest(new RpcRequest(nameof(GetTransaction), transactionId));
        }

        [HttpGet("listTransactions")]
        [Authorize(Policy = Permissions.ReadInfo)]
        public Task<object> ListTransactions(string account = "", int count = 10)
        {
            return MakeRpcRequest(new RpcRequest(nameof(ListTransactions), account, count));
        }

        [HttpGet("validateAddress/{address}")]
        [Authorize(Policy = Permissions.ReadInfo)]
        public Task<object> ValidateAddress(string address)
        {
            return MakeRpcRequest(new RpcRequest(nameof(ValidateAddress), address));
        }

        [HttpGet("getAccountAddress/{account}")]
        [Authorize(Policy = Permissions.CreateAddress)]
        public Task<object> GetAccountAddress(string account)
        {
            return MakeRpcRequest(new RpcRequest(nameof(GetAccountAddress), account));
        }

        [HttpPost("sendToAddress")]
        [Authorize(Policy = Permissions.CreateTransaction)]
        public Task<object> SendToAddress(Payment payment)
        {
            return MakeRpcRequest(new RpcRequest(nameof(SendToAddress), payment.Address, payment.Amount, payment.ExternalTransactionId));
        }

        private async Task<object> MakeRpcRequest(RpcRequest rpcRequest)
        {
            _logger.LogInformation($"{rpcRequest.Method} called");

            var json = JsonSerializer.Serialize(rpcRequest, _jsonSerializerOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = _http.CreateClient("gridcoin");
            var response = await client.PostAsync("/", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error connecting to Gridcoin wallet", new { response.StatusCode, Content = response.Content.ReadAsStringAsync() });

                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var responseContent = JsonSerializer.Deserialize<object>(responseBody);

            _logger.LogInformation($"{rpcRequest.Method} finished", responseContent);

            return Ok(responseContent);
        }
    }
}
