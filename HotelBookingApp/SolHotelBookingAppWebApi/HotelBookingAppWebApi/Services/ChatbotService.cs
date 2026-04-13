using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace HotelBookingAppWebApi.Services
{
    public interface IChatbotService
    {
        Task<string> SendAsync(ChatbotRequest request);
    }

    public class ChatbotService : IChatbotService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ChatbotService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> SendAsync(ChatbotRequest request)
        {
            var groqApiKey = _configuration["GroqApiKey"]
                ?? throw new InvalidOperationException("GroqApiKey not configured.");

            var groqUrl = "https://api.groq.com/openai/v1/chat/completions";

            var messages = new List<object>();

            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                messages.Add(new { role = "system", content = request.SystemPrompt });

            foreach (var msg in request.History ?? [])
                messages.Add(new { role = msg.Role == "model" ? "assistant" : msg.Role, content = msg.Text });

            messages.Add(new { role = "user", content = request.UserMessage });

            var body = new
            {
                model = "llama-3.1-8b-instant",
                messages,
                max_tokens = 512,
                temperature = 0.7
            };

            var json = JsonSerializer.Serialize(body);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, groqUrl);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", groqApiKey);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "Sorry, I could not get a response.";
        }
    }

    public class ChatbotRequest
    {
        public string UserMessage { get; set; } = string.Empty;
        public string? SystemPrompt { get; set; }
        public List<ChatHistoryItem>? History { get; set; }
    }

    public class ChatHistoryItem
    {
        public string Role { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
