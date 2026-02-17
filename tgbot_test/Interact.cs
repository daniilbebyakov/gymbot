using GymBot.Common.Constants;
using GymBot.Data;
using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using static GymBot.Common.Constants.BotCommands;
using static GymBot.Common.Constants.ToUserMessage;
using static GymBot.Common.Constants.BotMessages;

namespace GymBot
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
            string usermessage = update.Message.Text ?? String.Empty;
            switch (usermessage)
            {
                case (BotCommands.Start):
                    bool added = await _user.AddUserIfNotExist(update.Message.Chat.Id, update.Message.From?.Username ?? String.Empty);
                    if (added)
                    {
                        await client.SendMessage(update.Message.Chat.Id, ToUserMessage.RegistrationSuccess);
                    }
                    break;
                case (BotCommands.Me):
                    await client.SendMessage(update.Message.Chat.Id, string.Format(ToUserMessage.UserInfo, update.Message.Chat.Id, update.Message.From?.Username ?? "Нет ника"));
                    break;
                default:
                    await client.SendMessage(update.Message.Chat.Id, update.Message?.Text ?? BotMessages.BotMessageNoText);
                    break;
            }
        }
    }
}
