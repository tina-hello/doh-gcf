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

Without extra parameter, your proxy will resolve using Google's own DoH server. You can use other servers just by adding their short name/domain after the function name, eg : `https://region-projectname.cloudfunctions.net/functionname/shortname`

### Short names
* `adguard` [Adguard](http://adguard.com/) adblocking variant, if you just want to setup adblocking once without maintaining it.
* `quad9` [Quad9](https://www.quad9.net/) malware protection, if you're OK with ads or already have browser-based adblockers.
* `cloudflare` [Cloudflare](https://blog.cloudflare.com/introducing-1-1-1-1-for-families/)'s malware filter variant.
* `cleanbrowsing` [CleanBrowsing](https://cleanbrowsing.org/)'s Family Filter variant. Aside from blocking phising and malware sites, it also blocks adult sites, vpn, proxies, and mixed sites like Reddit.
* `nextdns` [NextDNS](http://nextdns.io/) infinitely customizable filter. On its own you get the non-filtering variant, add your configuration id after slash, eg : `nextdns/abcde`, for device identifier, just add another slash and your device name, eg : `nextdns/abcde/phone`.

### Custom servers

If the provider use default `/dns-query` path, just use their domain name without the path, so for OpenDNS standard service which is served through `https://doh.opendns.com/dns-query`, use `https://region-projectname.cloudfunctions.net/functionname/doh.opendns.com`

If the provider use non-standard path, include their path, so for CleanBrowsing adult filter that's served through `https://doh.cleanbrowsing.org/doh/adult-filter/`, use `https://region-projectname.cloudfunctions.net/functionname/doh.cleanbrowsing.org/doh/adult-filter` (remember to remove the trailing slash)

## Pros :

* Free for even heavy usages. With free 2 million calls and 5 GB egress traffic a month, this should be enough to serve even an entire household (for comparison, the free tier for NextDNS provides 300 thousand request a month).

* Simply create another project if your URL is blocked.

* No need to fiddle with command lines or setting up updates.

* Create multiple functions on any region you want with no additional cost.

* Proxy to any provider you want and utilize their filtering features, even your custom filter.

## Cons :

* Not the fastest solution, might take between 50-500 ms for each query, compared to 5-20 ms for average global resolvers. Browsers and OSes cache DNS request, but first visit might feel sluggish.

* Can't do custom filtering on itself. The code don't even try to parse the DNS request and just pass them as is. Either use existing provider filters or host your own (probably useful if direct access to your server is blocked/need to be kept secret)

* Can't provide extra privacy. Your IP is included in the request to the DoH server, along with complete information about the GCF instance. Don't use with DoH server you can't trust.

* Your network admin might just block the entire cloudfunctions.net domain. That would break sites that use GCF without whitelisting, viable for small organization level.
