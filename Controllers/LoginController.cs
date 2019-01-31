using hu.jia.webapi3.Handlers;
using hu.jia.webapi3.Models;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace hu.jia.webapi3.Controllers
{
    public class LoginController : ApiController
    {
        [HttpPost]
        public HttpResponseMessage Login(User user)
        {
            var userRepository = new List<string> {
                "User",
                "Admin"
            };

            if (!userRepository.Contains(user.Username))
            {
                return Request.CreateResponse(HttpStatusCode.NotFound, "The user was not found.");
            }

            bool credentials = (user.Password == "Pass");

            if (!credentials)
            {
                return Request.CreateResponse(HttpStatusCode.Forbidden, "The username/password combination was wrong.");
            }

            return Request.CreateResponse(HttpStatusCode.OK, TokenManager.GenerateToken(user.Username));
        }
    }
}
