# DNS over HTTPS proxy on Google Cloud Function
A DNS over HTTPS (DoH) resolver can be accessed entirely through a simple URL. If your ISP/organization blocks popular DoH provider, you don't trust random DoH proxy and can't be bothered to setup your own server, you can use this script to have your own DoH proxy on Google Cloud Function (GCF).

## Usage Steps

* Get to https://console.cloud.google.com/functions and create a new function. 

If you don't have a project yet, remember the project name will be part of the function subdomain and visible to network logs, so don't use project name that implies it's a DoH proxy. The function name will be the path, that shouldn't be visible to network logs, feel free to use shortest function name as possible (even a single letter will do).

* Pick the closest region to your location
* Change the authentication options to allow unauthorized invocations
* Take note of the function trigger URL, it will be in the pattern of `https://region-projectname.cloudfunctions.net/functionname`, click Save and Next
* In the new page, change the Runtime to .NET
* Replace the entire content of Function.cs with [my version](/Function.cs) then click deploy
* Use your function trigger URL anywhere DoH URL is accepted (Chrome, Firefox, Intra, macOS and iOS profile generator, etc)

Your proxy will resolve using Google's own 8.8.8.8. Feel free to change the code with your own, it could be any IPv4/IPv6 address of Do53 resolver, so even NextDNS custom configuration and Cloudflare Team can be used whatever your device IP are (you'll need to use IPv6 for NextDNS and Cloudflare custom configuration, your device can still use IPv4 to connect). If the resolver have ECS, it will use the the Google Cloud's IP and the query is unencrypted between Google Cloud and the resolver you choose. This version is essentially a port of [NotMikeDev/DoH](https://github.com/NotMikeDEV/DoH), just gaze upon its simplicity puny mortals!

