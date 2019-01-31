using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace hu.jia.webapi3.Handlers
{
    internal class SecurityHandler : DelegatingHandler
    {
        private static List<string> IgnoredList = new List<string>() {
            "/api/login",
            "/api/logout"
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (IgnoredList.Contains(request.RequestUri.PathAndQuery)) {
                // go to the next handler
                return base.SendAsync(request, cancellationToken);
            }

            var nonceQuery = request.GetQueryNameValuePairs().FirstOrDefault(item => string.Compare(item.Key, "nonce", true) == 0);
            var timestampQuery = request.GetQueryNameValuePairs().FirstOrDefault(item => string.Compare(item.Key, "timestamp", true) == 0);
            var signatureQuery = request.GetQueryNameValuePairs().FirstOrDefault(item => string.Compare(item.Key, "signature", true) == 0);

            if (string.IsNullOrWhiteSpace(nonceQuery.Value) || !Int32.TryParse(nonceQuery.Value, out int nonce))
            {
                return Task<HttpResponseMessage>.Factory.StartNew(() => new HttpResponseMessage(HttpStatusCode.BadRequest));
            }

            if (string.IsNullOrWhiteSpace(timestampQuery.Value) || !Int32.TryParse(timestampQuery.Value, out int timestamp))
            {
                return Task<HttpResponseMessage>.Factory.StartNew(() => new HttpResponseMessage(HttpStatusCode.BadRequest));
            }

            if (string.IsNullOrWhiteSpace(signatureQuery.Value))
            {
                return Task<HttpResponseMessage>.Factory.StartNew(() => new HttpResponseMessage(HttpStatusCode.BadRequest));
            }

            // go to the next handler
            return base.SendAsync(request, cancellationToken);
        }
    }
}