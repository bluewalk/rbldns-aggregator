using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DNS.Client;
using DNS.Client.RequestResolver;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using DNS.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Net.Bluewalk.DotNetEnvironmentExtensions;

namespace Net.Bluewalk.RblDnsAggregator
{
    public class Logic : IHostedService
    {
        private readonly ILogger _logger;
        private readonly DnsServer _dnsServer;

        private readonly string _dnsSuffix = EnvironmentExtensions.GetEnvironmentVariable("DNS_SUFFIX", "local");
        private readonly string _upstreamDns = EnvironmentExtensions.GetEnvironmentVariable("UPSTREAM_DNS", "8.8.8.8");
        private readonly string _rblList = EnvironmentExtensions.GetEnvironmentVariable("RBL_LIST", "bl.spamcop.net");
        private readonly int _listenPort = EnvironmentExtensions.GetEnvironmentVariable("PORT", 53);

        public Logic(ILogger<Logic> logger)
        {
            _logger = logger;

            _dnsServer = new DnsServer(
                new RblRequestResolver(_dnsSuffix, _upstreamDns, _rblList.Split(',', ';').ToList(),
                    _logger)
            );

            _dnsServer.Errored += (sender, args) => _logger.LogError(args.Exception, "An error occurred");
            _dnsServer.Listening += (sender, args) => _logger.LogInformation($"DNS server is listening at port {_listenPort}");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting DNS server");
            _logger.LogInformation($"DNS suffix: {_dnsSuffix}");
            _logger.LogInformation($"Upstream server: {_upstreamDns}");
            _logger.LogInformation($"RBL list: {_rblList}");

            _dnsServer.Listen(_listenPort);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping DNS server");
            _dnsServer.Dispose();
        }
    }

    public class RblRequestResolver : IRequestResolver
    {
        private class ResponseItem
        {
            public string Query { get; }
            public IPAddress Result { get; }
            public string Rbl { get; }
            public string RblMessage { get; }
            public DateTime Expires { get; }

            public ResponseItem(string query, string rbl, string rblMessage, IPAddress result)
            {
                Query = query;
                Result = result;
                Rbl = rbl;
                RblMessage = rblMessage;
                Expires = DateTime.Now.AddDays(1);
            }
        }

        private readonly string _dnsSuffix;
        private readonly DnsClient _dnsClient;
        private readonly List<string> _rblList;
        private readonly ILogger _logger;
        private readonly List<ResponseItem> _cache;

        public RblRequestResolver(string dnsSuffix, string upstream, List<string> rblList, ILogger logger)
        {
            _dnsSuffix = $".{dnsSuffix}";
            _dnsClient = new DnsClient(upstream);
            _rblList = rblList;
            _logger = logger;
            _cache = new List<ResponseItem>();
        }

        private TextResourceRecord SearchTxt(string query)
        {
            try
            {
                var txt = _dnsClient.Resolve(query, RecordType.TXT).Result;
                var rblMessage = txt.AnswerRecords
                    .Where(a => a.Type.Equals(RecordType.TXT))
                    .Cast<TextResourceRecord>()
                    .FirstOrDefault();

                return rblMessage;
            }
            catch
            {
                return null;
            }
        }

        private ResponseItem Search(string query, RecordType recordType)
        {
            _logger.LogInformation($"Checking blacklisting for {query}");
            var result = _cache.FirstOrDefault(c => c.Query.Equals(query));
            if (result != null)
            {
                _logger.LogInformation("Cached item found");
                return result;
            }

            if (recordType != RecordType.A && recordType != RecordType.AAAA) return null;

            foreach (var rbl in _rblList)
            {
                _logger.LogInformation($"Checking {rbl}");
                try
                {
                    var resolves = _dnsClient.Lookup($"{query}.{rbl}", recordType).Result;

                    if (!resolves.Any()) continue;
                    var rblMessage = SearchTxt($"{query}.{rbl}");

                    _logger.LogInformation("Found a match");
                    result = new ResponseItem(query, rbl, rblMessage?.ToStringTextData(), resolves.First());
                    _cache.Add(result);

                    return result;
                }
                catch (Exception e)
                {
                    // ignored
                }
            }

            _logger.LogInformation("No matches found");
            return null;
        }

        public Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            IResponse response = Response.FromRequest(request);

            foreach (var question in response.Questions)
            {
                if (!question.Name.ToString().EndsWith(_dnsSuffix)) continue;
                var query = question.Name.ToString().Replace(_dnsSuffix, string.Empty);

                var responseItem = Search(query, question.Type);

                if (responseItem == null) continue;

                switch (question.Type)
                {
                    case RecordType.A:
                    case RecordType.AAAA:
                        _logger.LogInformation($"A/AAAA response: {responseItem.Result}");

                        response.AnswerRecords.Add(new IPAddressResourceRecord(question.Name, responseItem.Result));
                        break;

                    case RecordType.TXT:
                        var txtResponse = responseItem.RblMessage ?? $"Blocked by {responseItem.Rbl}";

                        _logger.LogInformation($"TXT response: {txtResponse}");

                        response.AnswerRecords.Add(new TextResourceRecord(question.Name,
                            CharacterString.FromString(txtResponse)));
                        break;

                    default:
                        response.ResponseCode = ResponseCode.Refused;
                        break;
                }
            }

            if (!response.AnswerRecords.Any())
            {
                _logger.LogInformation("No matches found");
                response.ResponseCode = ResponseCode.NameError;
            }

            return Task.FromResult(response);
        }
    }
}
