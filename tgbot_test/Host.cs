using GymBot.Data;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using static GymBot.Common.Constants.BotMessages;

namespace GymBot
{

    public class Host
    {
        public Action<ITelegramBotClient, Update>? OnMessage;
        private readonly UserRepository _user;
        private readonly Interact _interact;
        private TelegramBotClient _bot;
        public Host(string token, UserRepository user, Interact interact)
        {
            _user = user;
            _bot = new TelegramBotClient(token);
            _interact = interact;
        }
        public void Start()
        {
            _bot.StartReceiving(UpdateHandler, ErrorHandler);
            Console.WriteLine(BotStarted);
        }

        private async Task ErrorHandler(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
        {
            Console.WriteLine(BotError + exception.Message);
            await Task.CompletedTask;
        }

        private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
        {
            Console.WriteLine(BotNewMessage + update.Message?.Text ?? BotMessageNoText);
            if (_interact != null)
                await _interact.OnMessage(client, update);
            await Task.CompletedTask;
        }
    }
}
