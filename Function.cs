using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.IO;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace SimpleHttpFunction
{
    public class Function : IHttpFunction
    {
        public async Task HandleAsync(HttpContext context)
        {

            byte[] datagram;

            if (context.Request.Method == "POST" && context.Request.ContentType == "application/dns-message")
            {
                using (var bodyStream = new MemoryStream())
                {
                    await context.Request.Body.CopyToAsync(bodyStream);
                    datagram = bodyStream.ToArray();
                }
            }
            else
            {
                var base64 = context.Request.Query["dns"].ToString();
                datagram = WebEncoders.Base64UrlDecode(base64);
            }


            context.Response.ContentType = "application/dns-message";

            var dnsPath = "dns-query";
            var dnsHost = "dns.google";

            var request = new HttpRequestMessage();
            request.Content = new ByteArrayContent(datagram);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message");

            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri($"https://{dnsHost}/{dnsPath}");
            request.Headers.Host = dnsHost;
            //comment the line below to disable ECS using client's IP
            request.Headers.Add("x-forwarded-for", context.Request.Headers["x-forwarded-for"].ToString());

            using (var client = new HttpClient())
            {
                using (var responseMessage = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted))
                {
                    context.Response.StatusCode = (int)responseMessage.StatusCode;
                    await responseMessage.Content.CopyToAsync(context.Response.Body);
                }
            }

        }

    }
}
