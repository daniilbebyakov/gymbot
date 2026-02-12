using GymBot.Data;
using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace tgbot_test
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
            Console.WriteLine("Бот запущен");
        }

        private async Task ErrorHandler(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
        {
            Console.WriteLine("Ошибка: "+exception.Message);
            await Task.CompletedTask;
        }

        private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
        {
            Console.WriteLine($"Пришло сообщение: {update.Message?.Text ?? "не текст"}");
            if (_interact!=null)
                await _interact.OnMessage(client,update);
            await Task.CompletedTask;
        }
    }
}
