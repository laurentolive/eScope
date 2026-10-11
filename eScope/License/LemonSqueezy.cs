using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace eScope.License
{
    public sealed class LsLicenseKey
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("key")] public string Key { get; set; } = "";
        [JsonPropertyName("activation_limit")] public int? ActivationLimit { get; set; }
        [JsonPropertyName("activation_usage")] public int ActivationUsage { get; set; }
        [JsonPropertyName("expires_at")] public DateTime? ExpiresAt { get; set; }
    }

    public sealed class LsInstance
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
    }

    public sealed class LsMeta
    {
        [JsonPropertyName("store_id")] public long StoreId { get; set; }
        [JsonPropertyName("product_id")] public long ProductId { get; set; }
        [JsonPropertyName("variant_name")] public string? VariantName { get; set; }
        [JsonPropertyName("customer_name")] public string? CustomerName { get; set; }
        [JsonPropertyName("customer_email")] public string? CustomerEmail { get; set; }
    }

    // common shape of the activate / validate / deactivate answers
    public sealed class LsResponse
    {
        [JsonPropertyName("activated")] public bool Activated { get; set; }
        [JsonPropertyName("valid")] public bool Valid { get; set; }
        [JsonPropertyName("deactivated")] public bool Deactivated { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("license_key")] public LsLicenseKey? LicenseKey { get; set; }
        [JsonPropertyName("instance")] public LsInstance? Instance { get; set; }
        [JsonPropertyName("meta")] public LsMeta? Meta { get; set; }
    }

    // the server could not be reached (no network, timeout, 5xx): keep the cached license
    public sealed class LicenseServerUnreachableException : Exception
    {
        public LicenseServerUnreachableException(string message, Exception? inner = null) : base(message, inner) { }
    }

    // Lemon Squeezy License API (https://docs.lemonsqueezy.com/api/license-api), no API key needed:
    // purchase, VAT, renewal and key generation are done by Lemon Squeezy, eScope only activates the key
    // on this machine and validates it again at startup
    public static class LemonSqueezy
    {
        // TODO: values of the Lemon Squeezy store (Settings > Stores for the id, product page for the id and the buy link)
        public const long StoreId = 0;
        public const long ProductId = 0;
        public const string BuyUrl = "https://escope.lemonsqueezy.com/buy/TODO";
        public const string PortalUrl = "https://escope.lemonsqueezy.com/billing";

        const string ApiUrl = "https://api.lemonsqueezy.com/v1/licenses/";

        static readonly HttpClient http = CreateClient();

        static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("eScope", global::eScope.eScope.AppVersion));
            return client;
        }

        public static Task<LsResponse> ActivateAsync(string key, string instanceName) =>
            PostAsync("activate", new() { ["license_key"] = key, ["instance_name"] = instanceName });

        public static Task<LsResponse> ValidateAsync(string key, string instanceId) =>
            PostAsync("validate", new() { ["license_key"] = key, ["instance_id"] = instanceId });

        public static Task<LsResponse> DeactivateAsync(string key, string instanceId) =>
            PostAsync("deactivate", new() { ["license_key"] = key, ["instance_id"] = instanceId });

        // the License API is shared by every Lemon Squeezy store: without this check a key of any other
        // product (even a free one of a store created for the purpose) would activate eScope Professional
        public static bool IsOurProduct(LsResponse r) =>
            r.Meta != null && r.Meta.StoreId == StoreId && r.Meta.ProductId == ProductId;

        static async Task<LsResponse> PostAsync(string action, Dictionary<string, string> form)
        {
            HttpResponseMessage resp;
            try
            {
                resp = await http.PostAsync(ApiUrl + action, new FormUrlEncodedContent(form));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new LicenseServerUnreachableException("License server unreachable: " + ex.Message, ex);
            }

            using (resp)
            {
                if ((int)resp.StatusCode >= 500)
                    throw new LicenseServerUnreachableException($"License server error {(int)resp.StatusCode}");

                // 4xx answers (unknown key, activation limit reached...) carry the same JSON with "error" set
                string body = await resp.Content.ReadAsStringAsync();
                try
                {
                    return JsonSerializer.Deserialize<LsResponse>(body)
                        ?? throw new LicenseServerUnreachableException("Empty answer from the license server");
                }
                catch (JsonException ex)
                {
                    throw new LicenseServerUnreachableException("Unexpected answer from the license server", ex);
                }
            }
        }
    }
}
