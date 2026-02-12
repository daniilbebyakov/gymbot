using GymBot.Data;
using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace tgbot_test
{
    public class Interact
    {
        private readonly UserRepository _user;

        public Interact(UserRepository user)
        {
            _user = user;
        }

        public async Task OnMessage(ITelegramBotClient client, Update update)
        {
            if (update.Message == null) return;
            string usermessage = update.Message.Text ?? "";
            switch (usermessage)
            {
                case "/start":
                    bool added = await _user.AddUserIfNotExist(update.Message.Chat.Id, update.Message.From?.Username ?? "");
                    if (added)
                    {
                        await client.SendMessage(update.Message.Chat.Id, "Теперь ты в файлах Эйпштена, пидар.");
                    }
                    break;
                case "/me":
                    await client.SendMessage(update.Message.Chat.Id, $"Твой id: {update.Message.Chat.Id}\nТвой ник: {update.Message.From?.Username ?? "Нет ника"}");
                    break;
                default:
                    await client.SendMessage(update.Message?.Chat.Id ?? 445584914, update.Message?.Text ?? "[не текст]");
                    break;
            }
        }
    }
}
