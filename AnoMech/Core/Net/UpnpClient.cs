using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace AnoMech.Core.Net;

public sealed record UpnpMapping(Uri ControlUrl, string ServiceType, int Port);

public sealed record UpnpOutcome(bool GatewayFound, bool Mapped, string? Error, IPAddress? RouterExternalIp, UpnpMapping? Mapping);

// Minimal Internet Gateway Device client: SSDP discovery, then SOAP calls on the WAN connection
// service. ponytail: discovery uses the default multicast interface only; a VPN adapter that
// owns the default route can hide the router — add per-interface discovery if players hit that.
public static class UpnpClient
{
    private const string SsdpAddress = "239.255.255.250";
    private const int SsdpPort = 1900;
    private static readonly string[] SearchTargets =
    [
        "urn:schemas-upnp-org:device:InternetGatewayDevice:2",
        "urn:schemas-upnp-org:device:InternetGatewayDevice:1",
    ];
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };

    public static async Task<UpnpOutcome> MapUdpPortAsync(int port, Action<string> log, CancellationToken ct)
    {
        List<Uri> locations;
        try
        {
            locations = await DiscoverAsync(TimeSpan.FromSeconds(3), ct);
        }
        catch (SocketException e)
        {
            log($"[Net] SSDP failed: {e.Message}");
            return new UpnpOutcome(false, false, $"無法送出 UPnP 探索封包（{e.Message}）", null, null);
        }
        if (locations.Count == 0)
            return new UpnpOutcome(false, false, "區網內沒有路由器回應 UPnP 探索", null, null);

        var lastError = "無法讀取路由器的 UPnP 描述";
        foreach (var location in locations)
        {
            try
            {
                var description = await Http.GetStringAsync(location, ct);
                if (!TryFindWanService(description, location, out var controlUrl, out var serviceType))
                {
                    lastError = "路由器沒有提供 WAN 連線服務（WANIPConnection／WANPPPConnection）";
                    continue;
                }

                var externalXml = await SoapAsync(controlUrl, serviceType, "GetExternalIPAddress", [], ct);
                IPAddress.TryParse(ParseSoapValue(externalXml, "NewExternalIPAddress"), out var externalIp);

                var addXml = await SoapAsync(controlUrl, serviceType, "AddPortMapping",
                [
                    ("NewRemoteHost", ""),
                    ("NewExternalPort", port),
                    ("NewProtocol", "UDP"),
                    ("NewInternalPort", port),
                    ("NewInternalClient", LocalAddressToward(controlUrl.Host)),
                    ("NewEnabled", 1),
                    ("NewPortMappingDescription", "AnoMech"),
                    ("NewLeaseDuration", 0),
                ], ct);
                if (ParseSoapFault(addXml) is { } fault)
                {
                    log($"[Net] UPnP AddPortMapping refused: {fault.Code} {fault.Description}");
                    return new UpnpOutcome(true, false, DescribeFault(fault), externalIp, null);
                }

                log($"[Net] UPnP mapped UDP {port} via {controlUrl}");
                return new UpnpOutcome(true, true, null, externalIp, new UpnpMapping(controlUrl, serviceType, port));
            }
            catch (Exception e) when (e is HttpRequestException or SocketException
                                      || (e is TaskCanceledException && !ct.IsCancellationRequested))
            {
                lastError = $"與路由器通訊失敗（{e.Message}）";
                log($"[Net] UPnP request to {location} failed: {e.Message}");
            }
        }
        return new UpnpOutcome(true, false, lastError, null, null);
    }

    public static async Task<bool> DeleteMappingAsync(UpnpMapping mapping, CancellationToken ct)
    {
        try
        {
            var xml = await SoapAsync(mapping.ControlUrl, mapping.ServiceType, "DeletePortMapping",
                [("NewRemoteHost", ""), ("NewExternalPort", mapping.Port), ("NewProtocol", "UDP")], ct);
            return ParseSoapFault(xml) is null;
        }
        catch (Exception e) when (e is HttpRequestException or SocketException or OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task<List<Uri>> DiscoverAsync(TimeSpan window, CancellationToken ct)
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        var target = new IPEndPoint(IPAddress.Parse(SsdpAddress), SsdpPort);
        foreach (var searchTarget in SearchTargets)
        {
            var message = Encoding.ASCII.GetBytes(
                $"M-SEARCH * HTTP/1.1\r\nHOST: {SsdpAddress}:{SsdpPort}\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: {searchTarget}\r\n\r\n");
            await udp.SendAsync(message, target, ct);
        }

        var found = new List<Uri>();
        using var listen = CancellationTokenSource.CreateLinkedTokenSource(ct);
        listen.CancelAfter(window);
        try
        {
            while (true)
            {
                var result = await udp.ReceiveAsync(listen.Token);
                if (TryParseSsdpLocation(Encoding.ASCII.GetString(result.Buffer), out var location) && !found.Contains(location))
                    found.Add(location);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
        }
        return found;
    }

    private static async Task<string> SoapAsync(Uri controlUrl, string serviceType, string action, (string Name, object Value)[] args, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, controlUrl)
        {
            Content = new StringContent(BuildSoapEnvelope(serviceType, action, args), Encoding.UTF8, "text/xml"),
        };
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{serviceType}#{action}\"");
        using var response = await Http.SendAsync(request, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    // UDP connect sends nothing; it only asks the OS which local address routes to the router.
    private static string LocalAddressToward(string host)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect(host, 1);
        return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
    }

    internal static bool TryParseSsdpLocation(string response, out Uri location)
    {
        location = null!;
        foreach (var rawLine in response.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var colon = line.IndexOf(':');
            if (colon <= 0 || !line[..colon].Trim().Equals("LOCATION", StringComparison.OrdinalIgnoreCase)) continue;
            if (Uri.TryCreate(line[(colon + 1)..].Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp)
            {
                location = uri;
                return true;
            }
        }
        return false;
    }

    internal static bool TryFindWanService(string descriptionXml, Uri location, out Uri controlUrl, out string serviceType)
    {
        controlUrl = location;
        serviceType = "";
        if (TryParseXml(descriptionXml) is not { } doc) return false;

        var urlBase = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "URLBase")?.Value.Trim();
        var baseUri = Uri.TryCreate(urlBase, UriKind.Absolute, out var parsedBase) ? parsedBase : location;
        var service = doc.Descendants()
            .Where(e => e.Name.LocalName == "service")
            .Select(e => (Type: Child(e, "serviceType"), Control: Child(e, "controlURL")))
            .Where(s => s.Type.Contains(":WANIPConnection:") || s.Type.Contains(":WANPPPConnection:"))
            .OrderBy(s => s.Type.Contains(":WANIPConnection:") ? 0 : 1)
            .FirstOrDefault();
        if (string.IsNullOrEmpty(service.Type) || string.IsNullOrEmpty(service.Control)) return false;
        if (!Uri.TryCreate(baseUri, service.Control, out var resolved)) return false;

        controlUrl = resolved;
        serviceType = service.Type;
        return true;
    }

    internal static string BuildSoapEnvelope(string serviceType, string action, (string Name, object Value)[] args)
    {
        var body = new StringBuilder();
        body.Append("<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body>");
        body.Append($"<u:{action} xmlns:u=\"{SecurityElement.Escape(serviceType)}\">");
        foreach (var (name, value) in args)
            body.Append($"<{name}>{SecurityElement.Escape(Convert.ToString(value, CultureInfo.InvariantCulture))}</{name}>");
        body.Append($"</u:{action}></s:Body></s:Envelope>");
        return body.ToString();
    }

    internal static string? ParseSoapValue(string xml, string element)
        => TryParseXml(xml)?.Descendants().FirstOrDefault(e => e.Name.LocalName == element)?.Value.Trim();

    internal static (int Code, string Description)? ParseSoapFault(string xml)
    {
        if (TryParseXml(xml) is not { } doc) return (0, "路由器回應無法解析");
        if (!doc.Descendants().Any(e => e.Name.LocalName == "Fault")) return null;
        var code = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "errorCode")?.Value.Trim();
        var description = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "errorDescription")?.Value.Trim() ?? "";
        return (int.TryParse(code, out var number) ? number : 0, description);
    }

    internal static string DescribeFault((int Code, string Description) fault) => fault.Code switch
    {
        718 => "這個埠已經被區網內其他裝置映射（錯誤 718）",
        606 => "路由器不允許這台電腦新增埠映射（錯誤 606，UPnP 權限不足）",
        725 => "路由器只接受永久映射（錯誤 725）",
        _ => $"錯誤 {fault.Code} {fault.Description}".TrimEnd(),
    };

    private static string Child(XElement element, string name)
        => element.Elements().FirstOrDefault(c => c.Name.LocalName == name)?.Value.Trim() ?? "";

    private static XDocument? TryParseXml(string xml)
    {
        try
        {
            return XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
