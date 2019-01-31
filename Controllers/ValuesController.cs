using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Web.Http;

namespace hu.jia.webapi3.Controllers
{
    [RoutePrefix("api/values")]
    public class ValuesController : ApiController
    {
        [HttpGet]
        [Route("")]
        [Authorize]
        public IEnumerable<string> Get()
        {
            return new[] { "Value1", "Value2", "Value3" };
        }

        [HttpPost]
        [Route("")]
        [Authorize]
        public IEnumerable<string> Add(string value)
        {
            return new[] { "Value1", "Value2", "Value3", "Value4", "Value5", value };
        }

        [HttpGet]
        [Route("{id}")]
        public HttpResponseMessage GetContent(int id)
        {
            string filename = "";
            string contentType = "text/html";

            switch (id)
            {
                case 1:
                    filename = "C:\\Projects\\hu.jia.webapi3\\wwwroot\\views\\lz4.html";
                    contentType = "text/html";
                    break;
                case 2:
                    filename = "C:\\Projects\\hu.jia.webapi3\\wwwroot\\js\\string.utility.js";
                    contentType = "application/javascript";
                    break;
                case 3:
                    filename = "C:\\Projects\\hu.jia.webapi3\\wwwroot\\js\\lz4\\lz4.js";
                    contentType = "application/javascript";
                    break;
                case 4:
                    filename = "C:\\Projects\\hu.jia.webapi3\\wwwroot\\js\\lz4\\lzw.js";
                    contentType = "application/javascript";
                    break;
                default:
                    break;
            }

            var content = File.ReadAllText(filename);
            var response = new HttpResponseMessage();

            if (content == null)
            {
                response.Content = new StringContent("Not Found", Encoding.UTF8, contentType);
                return response;
            }
            
            response.Content = new StringContent(content, Encoding.UTF8, contentType);

            return response;
        }
    }
}
