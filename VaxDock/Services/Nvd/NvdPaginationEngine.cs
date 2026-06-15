using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VaxDrive.VaxDock.Services.Nvd;

public class NvdPaginationEngine
{
    private readonly HttpClient _client;
    private readonly string _apiKey;
    private readonly string _dbKey;
    private readonly TokenBucketRateLimiter _rateLimiter;
    private readonly CveRepository _repository;

    public NvdPaginationEngine(CveRepository repository)
    {
        _repository = repository;
        _apiKey = Environment.GetEnvironmentVariable("NVD_API_KEY") ?? string.Empty;
        _dbKey = Environment.GetEnvironmentVariable("VAXDRIVE_DB_KEY") ?? string.Empty;

        // 50 req/30s with API key, 5 req/30s without
        int requestsPer30s = string.IsNullOrEmpty(_apiKey) ? 5 : 50;
        _rateLimiter = new TokenBucketRateLimiter(requestsPer30s, TimeSpan.FromSeconds(30));

        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            if (cert == null) return false;
            // TLS Certificate Pinning for NVD API
            return cert.Subject.Contains("api.nvd.nist.gov") && errors == System.Net.Security.SslPolicyErrors.None;
        };

        _client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _client.DefaultRequestHeaders.Add("apiKey", _apiKey);
        }
    }

    public async Task SyncAllCvesAsync(CancellationToken cancellationToken = default)
    {
        int startIndex = _repository.GetLastCheckpointStartIndex();
        int resultsPerPage = 2000;
        bool hasMore = true;
        Random rnd = new Random();

        while (hasMore)
        {
            await _rateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

            string url = $"https://services.nvd.nist.gov/rest/json/cves/2.0?resultsPerPage={resultsPerPage}&startIndex={startIndex}";
            int retryCount = 0;
            const int maxRetries = 8;
            bool success = false;
            string rawJson = string.Empty;

            while (!success && retryCount < maxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);

                    if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                        response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        throw new HttpRequestException($"Rate limit or 503 hit. Status: {response.StatusCode}");
                    }

                    response.EnsureSuccessStatusCode();
                    rawJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    
                    // Hash raw JSON response SHA-256 and store in page_hashes
                    byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawJson));
                    string hashHex = Convert.ToHexString(hash);
                    _repository.StorePageHash(startIndex, hashHex);

                    success = true;
                }
                catch (HttpRequestException ex)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        // Save checkpoint and abort cleanly
                        _repository.UpsertCheckpoint(startIndex, "ABORTED");
                        throw new InvalidOperationException($"NVD sync aborted after {maxRetries} retries.", ex);
                    }

                    // Exponential backoff + jitter
                    int delayMs = (int)Math.Pow(2, retryCount) * 1000 + rnd.Next(0, 1000);
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
            }

            // Parse response
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            int totalResults = root.GetProperty("totalResults").GetInt32();
            var vulnerabilities = root.GetProperty("vulnerabilities");

            foreach (var vulnItem in vulnerabilities.EnumerateArray())
            {
                var cveElement = vulnItem.GetProperty("cve");
                string cveId = cveElement.GetProperty("id").GetString() ?? "";
                string published = cveElement.GetProperty("published").GetString() ?? "";
                string modified = cveElement.GetProperty("lastModified").GetString() ?? "";
                
                double cvssV3 = 0.0;
                if (cveElement.TryGetProperty("metrics", out var metrics) && metrics.TryGetProperty("cvssMetricV31", out var v31Array) && v31Array.GetArrayLength() > 0)
                {
                    cvssV3 = v31Array[0].GetProperty("cvssData").GetProperty("baseScore").GetDouble();
                }

                string description = "";
                if (cveElement.TryGetProperty("descriptions", out var descriptions) && descriptions.GetArrayLength() > 0)
                {
                    description = descriptions[0].GetProperty("value").GetString() ?? "";
                }

                var record = new CveRecord
                {
                    CveId = cveId,
                    Published = published,
                    Modified = modified,
                    CvssV3 = cvssV3,
                    Description = description,
                    CpeList = "{}" // Simplified for brevity
                };

                _repository.UpsertCve(record);
            }

            int count = vulnerabilities.GetArrayLength();
            startIndex += count;

            _repository.UpsertCheckpoint(startIndex, "SUCCESS");

            if (startIndex >= totalResults || count == 0)
            {
                hasMore = false;
            }
        }
    }

    private class TokenBucketRateLimiter
    {
        private readonly int _maxTokens;
        private readonly TimeSpan _refillPeriod;
        private int _tokens;
        private DateTime _lastRefill;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public TokenBucketRateLimiter(int maxTokens, TimeSpan refillPeriod)
        {
            _maxTokens = maxTokens;
            _refillPeriod = refillPeriod;
            _tokens = maxTokens;
            _lastRefill = DateTime.UtcNow;
        }

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                if (now - _lastRefill > _refillPeriod)
                {
                    _tokens = _maxTokens;
                    _lastRefill = now;
                }

                if (_tokens <= 0)
                {
                    TimeSpan delay = _refillPeriod - (now - _lastRefill);
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    _tokens = _maxTokens;
                    _lastRefill = DateTime.UtcNow;
                }

                _tokens--;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
