using HotelBookingAppWebApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelBookingAppWebApi.Controllers
{
    [ApiController]
    [Route("api/chatbot")]
    public class ChatbotController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;

        public ChatbotController(IChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatbotRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserMessage))
                return BadRequest(new { message = "UserMessage is required." });

            var reply = await _chatbotService.SendAsync(request);
            return Ok(new { reply });
        }
    }
}
