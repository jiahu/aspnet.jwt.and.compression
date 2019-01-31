using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace hu.jia.webapi3.Handlers
{
    internal class TokenValidationHandler : DelegatingHandler
    {
        private bool TryRetrieveToken(HttpRequestMessage request, out string token)
        {
            token = null;

            if (request.Headers.TryGetValues("Authorization", out IEnumerable<string> authzHeaders) && authzHeaders.Count() == 1)
            {
                var bearerToken = authzHeaders.ElementAt(0);
                token = bearerToken.StartsWith("Bearer ") ? bearerToken.Substring(7) : bearerToken;

                return true;
            }

            var tokenQuery = request.GetQueryNameValuePairs().FirstOrDefault(item => string.Compare(item.Key, "token", true) == 0);

            if (!string.IsNullOrWhiteSpace(tokenQuery.Value))
            {
                token = tokenQuery.Value;

                return true;
            }

            return false;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpStatusCode statusCode;

            //determine whether a jwt exists or not
            if (!TryRetrieveToken(request, out string token))
            {
                statusCode = HttpStatusCode.Unauthorized;
                //allow requests with no token - whether a action method needs an authentication can be set with the claimsauthorization attribute
                return base.SendAsync(request, cancellationToken);
            }


            try
            {
                if (!TokenManager.ValidateToken(token))
                {
                    statusCode = HttpStatusCode.Unauthorized;
                    return Task<HttpResponseMessage>.Factory.StartNew(() => new HttpResponseMessage(statusCode) { });
                }

                //extract and assign the user of the jwt
                Thread.CurrentPrincipal = TokenManager.GetPrincipal(token);
                HttpContext.Current.User = TokenManager.GetPrincipal(token);

                return base.SendAsync(request, cancellationToken);
            }
            catch (SecurityTokenValidationException e)
            {
                statusCode = HttpStatusCode.Unauthorized;
            }
            catch (Exception ex)
            {
                statusCode = HttpStatusCode.InternalServerError;
            }

            return Task<HttpResponseMessage>.Factory.StartNew(() => new HttpResponseMessage(statusCode) { });
        }

        public bool LifetimeValidator(DateTime? notBefore, DateTime? expires, SecurityToken securityToken, TokenValidationParameters validationParameters)
        {
            if (expires != null)
            {
                if (DateTime.UtcNow < expires)
                {
                    return true;
                }
            }

            return false;
        }


    }


    public class TokenManager
    {
        private static string Secret = "XCAP05H6LoKvbRRa/QkqLNMI7cOHguaRyHzyg7n5qEkGjQmtBhz4SzYh4Fqwjyi3KJHlSXKPwVu2+bXr6CtpgQ==";

        public static string GenerateToken(string username)
        {
            byte[] key = Convert.FromBase64String(Secret);
            SymmetricSecurityKey securityKey = new SymmetricSecurityKey(key);
            SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] {
                      new Claim(ClaimTypes.Name, username)}),
                Expires = DateTime.UtcNow.AddMinutes(60 * 30),
                SigningCredentials = new SigningCredentials(securityKey,
                SecurityAlgorithms.HmacSha256Signature)
            };

            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            JwtSecurityToken token = handler.CreateJwtSecurityToken(descriptor);
            token.Payload["Role"] = "Admin";

            return handler.WriteToken(token);
        }

        public static ClaimsPrincipal GetPrincipal(string token)
        {
            try
            {
                JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
                JwtSecurityToken jwtToken = (JwtSecurityToken)tokenHandler.ReadToken(token);

                if (jwtToken == null)
                {
                    return null;
                }

                byte[] key = Convert.FromBase64String(Secret);
                TokenValidationParameters parameters = new TokenValidationParameters()
                {
                    RequireExpirationTime = true,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };
                ClaimsPrincipal principal = tokenHandler.ValidateToken(token,
                      parameters, out SecurityToken securityToken);

                return principal;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public static bool ValidateToken(string token)
        {
            ClaimsPrincipal principal = GetPrincipal(token);

            if (principal == null)
            {
                return false;
            }

            ClaimsIdentity identity = null;

            try
            {
                identity = (ClaimsIdentity)principal.Identity;
            }
            catch (NullReferenceException)
            {
                return false;
            }

            Claim usernameClaim = identity.FindFirst(ClaimTypes.Name);

            return !string.IsNullOrWhiteSpace(usernameClaim.Value);
        }

    }
}