using hu.jia.webapi3.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace hu.jia.webapi3
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.MessageHandlers.Insert(0, new CompressionHandler());
            config.MessageHandlers.Add(new SecurityHandler());
            config.MessageHandlers.Add(new TokenValidationHandler());

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
