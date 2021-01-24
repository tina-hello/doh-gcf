using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace SimpleHttpFunction
{
    public class Function : IHttpFunction
    {
        public async Task HandleAsync(HttpContext context)
        {
            var request = new HttpRequestMessage();

            if (context.Request.Method == "POST")
            {
                request.Content = new StreamContent(context.Request.Body);
                request.Method = HttpMethod.Post;
            }
            else //RFC 8484 only specify GET and POST
            {
                request.Method = HttpMethod.Get;
            }

            foreach (var header in context.Request.Headers)
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
            //the following are the default values, feel free to replace them if you want to use other provider
            //without adding their path
            var dnsPath = "dns-query";
            var dnsHost = "dns.google";
            var originPath = context.Request.Path.ToString();
            var secondSlashIndex = originPath.IndexOf("/");
            //disable this block entirely if you don't want to support other provider at all
            if (secondSlashIndex >= 0 && secondSlashIndex < originPath.Length - 1)
            {
                var option = originPath[(secondSlashIndex + 1)..];
                switch (option)
                {
                    case "adguard": //block ads, trackers and phishing 
                        dnsHost = "dns.adguard.com";
                        break;
                    case "cloudflare": //malware filtering
                        dnsHost = "security.cloudflare-dns.com";
                        break;
                    case "quad9": //malware filtering
                        dnsHost = "dns.quad9.net";
                        break;
                    case "cleanbrowsing": //block adult, proxy, VPN and mixed sites like Reddit. Force search engine and Youtube to safe mode. 
                        dnsHost = "doh.cleanbrowsing.org";
                        dnsPath = "doh/family-filter"; //remember to replace dnsPath if needed
                        break;
                    case "spread": //randomly pick one of the unrestricted server, because filtering server will just make it harder to debug problems
                        var hosts = new[] { "dns.google", "dns.cloudflare.com", "dns10.quad9.net", "dns-unfiltered.adguard.com" };
                        dnsHost = hosts[new Random().Next(hosts.Length)];
                        //dnsPath is unchanged, so only DoH server that use default /dns-query path are supported
                        break;
                    default: //use custom DNS
                        var thirdSlashIndex = option.IndexOf("/");
                        if (thirdSlashIndex > 0)
                        {
                            dnsHost = option.Substring(0, thirdSlashIndex);
                            dnsPath = option.Substring(thirdSlashIndex + 1);
                        }
                        else
                        {
                            dnsHost = option;
                        }
                        if (dnsHost == "nextdns") //still use the custom dns path to support nextdns custom filtering
                        {
                            dnsHost = "dns.nextdns.io";
                        }
                        break;
                }
            }

            request.RequestUri = new Uri($"https://{dnsHost}/{dnsPath}{context.Request.QueryString}");
            request.Headers.Host = dnsHost;
            
            using (var client = new HttpClient())
            {
                using (var responseMessage = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted))
                {
                    context.Response.StatusCode = (int)responseMessage.StatusCode;
                    foreach (var header in responseMessage.Headers)
                    {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }

                    foreach (var header in responseMessage.Content.Headers)
                    {
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    }
                    context.Response.Headers.Remove("transfer-encoding");
                    await responseMessage.Content.CopyToAsync(context.Response.Body);
                }
            }
        }
    }
}
