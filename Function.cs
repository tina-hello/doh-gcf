using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System;

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
                var do53Address = pickDo53(option);

                if (datagram.Result == null)
                {
                    context.Response.StatusCode = 400;
                    return;
                }

                if (do53Address != null)
                {  
                    await sendDo53(context, datagram.Result, do53Address);
                    return;
                }
               
                var (dnsHost,dnsPath) = extractUrlDoH(option);

                await sendDoH(context, datagram.Result, dnsHost, dnsPath);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message + Environment.NewLine + ex.StackTrace);
            }

        }

        private static (string dnsHost,string dnsPath) extractUrlDoH(string option)
        {
            
            if (dohProviders.ContainsKey(option))
            {
                option = dohProviders[option];
            }
            else if (option.StartsWith("nextdns"))
            {
                option = option.Replace("nextdns", "dns.nextdns.io");
            }
            else switch (option)
            {
                    case "doh-unrestricted":
                        option = dohProviders["doh-"+unrestricted[rand.Next(unrestricted.Length)]];
                        break;
                    case "doh-malware":
                        option = dohProviders["doh-"+antiMalware[rand.Next(antiMalware.Length)]];
                        break;
                    case "doh-family":
                        option = dohProviders["doh-"+family[rand.Next(family.Length)]];
                        break;
            }
            var slashIndex = option.IndexOf("/");
            string dnsHost, dnsPath;
            if (slashIndex > 0)
            {
                dnsHost = option.Substring(0, slashIndex);
                dnsPath = option[(slashIndex + 1)..];
            }
            else
            {
                dnsHost = option; 
                dnsPath= ""; //if you set this to "dns-query", 
                             //you don't need to specify the path for most DoH but break providers like commons.host
            }
           
            return (dnsHost, dnsPath);
        }

        private static async Task sendDoH(HttpContext context, byte[] datagram, string dnsHost, string dnsPath)
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

        private static readonly Dictionary<string, IPAddress> do53Providers = new Dictionary<string, IPAddress> ()
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

        private static readonly string[] unrestricted = new[] { "adguard-unrestricted", "cloudflare", "quad9-unrestricted", "google" };
        private static readonly string[] antiMalware  = new[] { "adguard", "cleanbrowsing-security", "cloudflare-malware", "quad9" };
        private static readonly string[] family       = new[] { "adguard-family", "cleanbrowsing-family", "cloudflare-adult", "opendns-family" };

        private static readonly Dictionary<string, string> dohProviders = new Dictionary<string, string>()
        {
            ["doh-google"]                  = "dns.google/dns-query",
            ["doh-adguard"]                 = "dns.adguard.com/dns-query",
            ["doh-adguard-family"]          = "dns-family.adguard.com/dns-query",
            ["doh-adguard-unrestricted"]    = "dns-unfiltered.adguard.com/dns-query",
            ["doh-cleanbrowsing-family"]    = "doh.cleanbrowsing.org/doh/family-filter/",
            ["doh-cleanbrowsing-adult"]     = "doh.cleanbrowsing.org/doh/adult-filter/",
            ["doh-cleanbrowsing-security"]  = "doh.cleanbrowsing.org/doh/security-filter/",
            ["doh-cloudflare"]              = "dns.cloudflare.com/dns-query",
            ["doh-cloudflare-malware"]      = "security.cloudflare-dns.com/dns-query",
            ["doh-cloudflare-adult"]        = "family.cloudflare-dns.com/dns-query",
            ["doh-opendns"]                 = "doh.opendns.com/dns-query",
            ["doh-opendns-family"]          = "doh.familyshield.opendns.com/dns-query",
            ["doh-quad9"]                   = "dns.quad9.net/dns-query",
            ["doh-quad9-unrestricted"]      = "dns10.quad9.net/dns-query",
            ["doh-quad9-ecs"]               = "dns11.quad9.net/dns-query",
        };

        private static readonly Random rand = new Random();
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

            if (option.StartsWith("nextdns-"))
            {
                var configID = option.Replace("nextdns-", "");
                return IPAddress.Parse($"2a07:a8c0::{configID[..^4]}:{configID[^4..]}");
            }

            if (do53Providers.ContainsKey(option))
            {
                return do53Providers[option];
            }
            
            switch (option)
            {
                case "unrestricted":
                    return do53Providers[unrestricted[rand.Next(unrestricted.Length)]];
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
