using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Net.Sockets;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace SimpleHttpFunction
{
    public class Function : IHttpFunction
    {
        private readonly ILogger _logger;

        public Function(ILogger<Function> logger) =>
            _logger = logger;
        public async Task HandleAsync(HttpContext context)
        {
            try
            {

                var datagram = extractDatagram(context);

                var option = extractOption(context);

                context.Response.ContentType = "application/dns-message";

                IPAddress do53Address;
                do53Address = pickDo53(option);
                
                if (do53Address != null)
                {
                    if (datagram.Result == null)
                    {
                        context.Response.StatusCode = 400;
                        return;
                    }
                    await sendDo53(context, datagram.Result, do53Address);
                    return;
                }

                var dnsPath = "";
                var dnsHost = "";

                var thirdSlashIndex = option.IndexOf("/");
                if (thirdSlashIndex > 0)
                {
                    dnsHost = option.Substring(0, thirdSlashIndex);
                    dnsPath = option.Substring(thirdSlashIndex + 1);
                }
                else
                {
                    dnsHost = option;
                    dnsPath = "";
                }


                await sendDoH(context, datagram.Result, dnsPath, dnsHost);
                return;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message + Environment.NewLine + ex.StackTrace);
            }

        }

        private async static Task sendDoH(HttpContext context, byte[] datagram, string dnsPath, string dnsHost)
        {
            var request = new HttpRequestMessage();
            request.Content = new ByteArrayContent(datagram);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/dns-message");

            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri($"https://{dnsHost}/{dnsPath}");
            request.Headers.Host = dnsHost;
            //comment the line below to disable ECS using client's IP
            request.Headers.Add("x-forwarded-for",context.Request.Headers["x-forwarded-for"].ToString());

            using (var client = new HttpClient())
            {
                using (var responseMessage = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted))
                {
                    context.Response.StatusCode = (int)responseMessage.StatusCode;
                    await responseMessage.Content.CopyToAsync(context.Response.Body);
                }
            }
        }

        private async static Task sendDo53(HttpContext context, byte[] datagram, IPAddress do53Address)
        {  
            var endpoint = new IPEndPoint(do53Address, 53);
            var udpClient = new UdpClient(endpoint.AddressFamily);
            udpClient.Connect(endpoint);
            udpClient.Send(datagram, datagram.Length);

            var result = udpClient.Receive(ref endpoint);
            await context.Response.Body.WriteAsync(result, 0, result.Length);
        }

        private static Dictionary<string, IPAddress> do53Providers = new Dictionary<string, IPAddress> ()
        {
            ["google"]                  = IPAddress.Parse("8.8.8.8"),
            ["google6"]                 = IPAddress.Parse("2001:4860:4860::8888"),
            ["adguard"]                 = IPAddress.Parse("94.140.14.14"),
            ["adguard-family"]          = IPAddress.Parse("94.140.14.15"),
            ["adguard-unrestricted"]    = IPAddress.Parse("94.140.14.140"),
            ["cleanbrowsing-family"]    = IPAddress.Parse("185.228.168.168"),
            ["cleanbrowsing-adult"]     = IPAddress.Parse("185.228.168.10"),
            ["cleanbrowsing-security"]  = IPAddress.Parse("185.228.168.9"),
            ["cloudflare"]              = IPAddress.Parse("1.1.1.1"),
            ["cloudflare-malware"]      = IPAddress.Parse("1.1.1.2"),
            ["cloudflare-adult"]        = IPAddress.Parse("1.1.1.3"),
            ["opendns"]                 = IPAddress.Parse("208.67.222.222"),
            ["opendns-family"]          = IPAddress.Parse("208.67.222.123"),
            ["quad9"]                   = IPAddress.Parse("9.9.9.9"),
            ["quad9-unrestricted"]      = IPAddress.Parse("9.9.9.10"),
            ["quad9-ecs"]               = IPAddress.Parse("9.9.9.11"),
        };

        static string[] unrestricted = new[] { "adguard-unrestricted", "cloudflare", "quad9-unrestricted" };
        static string[] ecsProviders = new[] { "google", "opendns", "quad9-ecs" };
        static string[] antiMalware = new[] { "adguard", "cleanbrowsing-security", "cloudflare-malware", "quad9" };
        static string[] family = new[] { "adguard-family", "cleanbrowsing-family", "cloudflare-adult", "opendns-family" };

        private static IPAddress pickDo53(string option)
        {
            if (!option.Any())
            {
                return do53Providers["google"];
            }
            IPAddress do53Address;
            
            if (IPAddress.TryParse(option,out do53Address))
            {
                return do53Address;
            }

            if (option.StartsWith("nextdns"))
            {
                var configID = option.Replace("nextdns", "");
                return IPAddress.Parse($"2a07:a8c0::{configID[..^4]}:{configID[^4..]}");
            }

            if (do53Providers.ContainsKey(option))
            {
                return do53Providers[option];
            }

            

            var rand = new Random();
            switch (option)
            {
                case "unrestricted":
                    return do53Providers[unrestricted[rand.Next(unrestricted.Length)]];
                case "ecs":
                    return do53Providers[ecsProviders[rand.Next(ecsProviders.Length)]];
                case "malware":
                    return do53Providers[antiMalware[rand.Next(antiMalware.Length)]];
                case "family":
                    return do53Providers[family[rand.Next(family.Length)]];
                default:
                    return null;
            }

            
        }

        private static string extractOption(HttpContext context)
        {
            var originPath = context.Request.Path.ToString();
            
            var secondSlashIndex = originPath.IndexOf("/");
            var option = "";
            if (secondSlashIndex >= 0 && secondSlashIndex < originPath.Length - 1)
            {
                option = originPath[(secondSlashIndex + 1)..];
            }
            
            return option;
        }

        private static async Task<byte[]> extractDatagram(HttpContext context)
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
            else if (context.Request.Query["dns"].Any())
            {
                var base64 = context.Request.Query["dns"].ToString();
                datagram = WebEncoders.Base64UrlDecode(base64);
            }
            else
            {
                datagram = null;
            }

            return datagram;
        }
    }
}
