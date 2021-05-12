# DNS over HTTPS proxy on Google Cloud Function
A DNS over HTTPS (DoH) resolver can be accessed entirely through a simple URL. If your ISP/organization blocks popular DoH provider, you don't trust random DoH proxy and can't be bothered to setup your own server, you can use this script to have your own DoH proxy on Google Cloud Function (GCF).

If you find the following too complex when all you need is simple resolver to target one server or need a straightforward example to implement DoH, see the [simpleDo53](https://github.com/tina-hello/doh-gcf/tree/simpleDo53) and [simpleDoH](https://github.com/tina-hello/doh-gcf/tree/simpleDoH) branch, or their original inspiration [NotMikeDEV/DoH](https://github.com/NotMikeDEV/DoH)

Don't have a card to activate Google Cloud account or just don't want to use Google? I have a very [simplistic port](https://github.com/tina-hello/doh-cf-workers) for Cloudflare, no card needed.

## Usage Steps

* Get to https://console.cloud.google.com/functions and create a new function. 

If you don't have a project yet, the project name will be part of the function subdomain and visible to network logs, so don't use project name that implies it's a DoH proxy. The function name will be the path, that shouldn't be visible to network logs, feel free to use shortest function name as possible (even a single letter will do).

* Pick the closest region to your location
* Change the authentication options to allow unauthorized invocations
* Take note of the function trigger URL, it will be in the pattern of `https://region-projectname.cloudfunctions.net/functionname`, click Save and Next
* In the new page, change the Runtime to .NET
* Replace the entire content of Function.cs with [my version](/Function.cs) then click deploy
* Use your function trigger URL anywhere DoH URL is accepted (Chrome, Firefox, Intra, macOS and iOS profile generator, etc)

Without extra parameter, your proxy will resolve using Google's own 8.8.8.8. You can use other servers just by adding their short name/domain after the function name, eg : `https://region-projectname.cloudfunctions.net/functionname/shortname`

### Short names

Used on its own, the short names will forward your query unencrypted through Do53. You can add `doh-` prefix, eg, `doh-google` (except `google6`, since the DoH always use IPv6 when available) to forward with encryption through DoH instead.

* `google` [Google](https://developers.google.com/speed/public-dns) public DNS. Fastest since the proxy already running inside Google's network
* `google6` IPv6 variant of the Google DNS. I put it mostly to hint how to use your own IPv6 address by editing the source code.
* `adguard` [Adguard](http://adguard.com/) blocks ads, trackers & phishing. Conservative filter means it just works without further maintainance.
* `adguard-family` the regular variant plus adult sites filtering. Decent for work/public sites purposes.
* `adguard-unrestricted` non filtering variant.
* `cleanbrowsing-family` [CleanBrowsing](https://cleanbrowsing.org/)'s most restrictive filter, aside from blocking phising and malware sites, it also blocks adult sites, vpn, proxies, and mixed sites like Reddit. Great for religious or educational institute.
* `cleanbrowsing-adult` only block adult, phishing and malware sites. Good for work purposes or personal use.
* `cleanbrowsing-security` only block phishing and malware sites.
* `cloudflare` [Cloudflare](https://blog.cloudflare.com/dns-resolver-1-1-1-1/)'s non-filtering variant.
* `cloudflare-malware` [Cloudflare](https://blog.cloudflare.com/introducing-1-1-1-1-for-families/)'s malware filter variant.
* `cloudflare-adult` Block adult sites in addition to malware filter.
* `opendns` [OpenDNS](https://support.opendns.com/hc/en-us/articles/227986707-Understanding-Malware-and-how-OpenDNS-helps) default malware blocking variant
* `opendns-family` blocks ["tasteless, proxy/anonymizer, sexuality and pornography"](https://support.opendns.com/hc/en-us/articles/228006487-FamilyShield-Router-Configuration-Instructions). They don't [consider Playboy](https://domain.opendns.com/playboy.com) as porn site, which may be a good or bad thing depends on your goal.
* `quad9` [Quad9](https://www.quad9.net/) malware protection, frequently [topping malware blocking test](https://www.quad9.net/dns-blocking-effectiveness-recent-independent-tests/)
* `quad9-ecs` with ECS support. Note however, since the DNS payload is sent as is through the cloud function, the detected IP will be Google Cloud's.
* `quad9-unrestricted` non filtering variant.

### [NextDNS](http://nextdns.io/) Configuration

Use `nextdns-` for Do53 mode or `nextdns/` for DoH mode, followed with your configuration ID, eg. if your configuration ID is abc123, use either `nextdns-abc123` or `nextdns/abc123` so the URL is going to be `https://region-projectname.cloudfunctions.net/functionname/nextdns-abc123` or `https://region-projectname.cloudfunctions.net/functionname/nextdns/abc123`. Do53 request will be sent to your NextDNS assigned IPv6 address, while DoH mode use your assigned DoH address. Device identifier is also supported in DoH mode, so if you use `nextdns/abc123/deviceid`, the request will be marked as coming from `deviceid` in the NextDNS log. If you activate ECS support, on Do53 mode the IP seen by the resolver is Google Cloud's, while the DoH mode will use your actual device IP. Use the Cache Boost and CNAME Flattening to reduce calls.

### Custom Do53 and DoH servers

Any resolvers that support Do53 can be used by their IP, eg. to use Google's 8.8.8.8, use `https://region-projectname.cloudfunctions.net/functionname/8.8.8.8`, IPv6 is also supported (even if your own connection is IPv4 only, Google Cloud can use them), so for Google's IPv6 variant, use `https://region-projectname.cloudfunctions.net/functionname/2001:4860:4860::8888`

Any resolvers that support DoH can be used by their complete domain and query path, eg to use Google's DoH variant, use `https://region-projectname.cloudfunctions.net/functionname/dns.google/dns-query`. The `/dns-query` part can be skipped, so just the domain is enough for resolvers that use the standard path.

The X-Forwarded-For header is set with your device's IP on DoH request, so ECS *might* work with the device's IP, but so far it's only confirmed on NextDNS, while all other ECS-capable resolver doesn't seem to care, or in OpenDNS case, return an entirely different answer. You can disable the header from the source code from a marked comment inside `sendDoH()`.

### Random Mode

There are three group of resolvers that you can pick to resolve queries. Each query would be resolved by different member, reducing the amount of data each resolver have of your activity. Use the group name as short code. Prepend with `doh-` to use the DoH variant.

* `unrestricted` : The unrestricted, non-filtering variants of Adguard, Quad9, Cloudflare and Google.

* `malware` : Anti malware variants of AdGuard, CleanBrowsing, Cloudflare and Quad9.

* `family` : Family friendly variants of AdGuard, CleanBrowsing, Cloudflare, and OpenDNS.

## Pros :

* Free for even heavy usages. With free 2 million calls and 5 GB egress traffic a month, this should be enough to serve even an entire household (for comparison, the free tier for NextDNS provides 300 thousand request a month).

* Automatically adjust for heavy load by launching more instance.

* Simply create another project if your URL is blocked.

* No need to fiddle with command lines or setting up updates.

* Create multiple functions on any region you want with no additional cost. Free quota is shared across all functions.

* Proxy to any provider you want and utilize their filtering features, even your custom filter.

* Relatively fast when proxying to Do53 resolver.

* Can still use ECS when proxying to DoH resolver.

* On DoH mode, your query is encrypted nearly all the way from your device to the resolver, except during the transit in Google Cloud.

* You can use IPv6 and IPv4 resolvers even if your device connection doesn't support them. Useful for IPv4-only ISP or when running IPv6-only VM

* If you're paranoid about DNS resolvers tracking your request, the Do53 mode will only send the datagram with no identifying information aside from the cloud instance IPv6 address (*might* be correlated through requests) without the resolver getting your client IP.

## Cons :

* Not the fastest solution, might take between 10-200 ms for Do53 and 50-200 ms for DoH , compared to 5-20 ms for average global resolvers. Browsers and OSes cache DNS request, but first visit might feel sluggish. Spinning up a new instance (after extended idle or during heavy load) can take 2-5 seconds.

* Can't do custom filtering on itself. The code don't even try to parse the DNS request and just pass them as is. Either use existing provider filters or host your own (probably useful if direct access to your server is blocked/need to be kept secret). Aside from NextDNS which have excellent adblocking filters, there's free tier for [Cloudlare Team](https://www.cloudflare.com/teams/) with unlimited queries for multiple configuration and 50 members.

* Your network admin might just block the entire cloudfunctions.net domain. That would break sites that use GCF without whitelisting, viable for small organization level.

* If you're really paranoid about privacy, this runs on Google Cloud, owned by the largest advertiser in the world.
