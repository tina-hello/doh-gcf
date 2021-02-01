using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;

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

            var do53Address = IPAddress.Parse("8.8.8.8"); //IPv6 are welcome too

            var endpoint = new IPEndPoint(do53Address, 53);
            var udpClient = new UdpClient(endpoint.AddressFamily);
            udpClient.Connect(endpoint);
            udpClient.Send(datagram, datagram.Length);

            var result = udpClient.Receive(ref endpoint);
            await context.Response.Body.WriteAsync(result, 0, result.Length);
        }

    }
}
