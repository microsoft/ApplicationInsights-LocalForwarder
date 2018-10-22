using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace W3CService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ForwardController : ControllerBase
    {
        private readonly HttpClient httpClient;
        public ForwardController(HttpClient httpclient)
        {
            this.httpClient = httpclient;
        }

        private async Task<string> CallNextAsync(string url, Data[] arguments)
        {
            if (url != null)
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(arguments), Encoding.UTF8, "application/json")
                };
                var response = await httpClient.SendAsync(request);
                return await response.Content.ReadAsStringAsync();
            }

            return "all done";
        }

        // POST api/values
        [HttpPost]
        public async Task<string> Post([FromBody]Data[] data)
        {
            var result = string.Empty;

            if (data != null)
            {
                foreach (var argument in data)
                {
                    if (argument.sleep != null)
                    {
                        result = "slept for " + argument.sleep.Value + " ms";
                        await Task.Delay(argument.sleep.Value);
                    }

                    result += await CallNextAsync(argument.url, argument.arguments);
                }
            }
            else
            {
                result = "done";
            }

            return result;
        }
    }

    public class Data
    {
        public int? sleep { get; set; }
        public string url { get; set; }
        public Data[] arguments { get; set; }
    }
}
