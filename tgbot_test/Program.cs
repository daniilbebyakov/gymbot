using Telegram.Bot;
using Telegram.Bot.Types;
using tgbot_test;
using Npgsql;
using GymBot.Data;
using System.Runtime.InteropServices;

internal class Program
{
    private static void Main(string[] args)
    {
        Host gymbot = new Host("8403810096:AAE5YTeO8IYbxWFKC_v79WyiqvqvfArz3lM");
        gymbot.Start();
        gymbot.OnMessage += OnMessage;
        Console.ReadLine(); 
    }

    static UserRepository Users = new UserRepository();
    private static async void OnMessage(ITelegramBotClient client, Update update)
    {
        if (update.Message == null) return;
        string usermessage = update.Message.Text ?? "";
        switch (usermessage)
        {
            case "/start":
                bool added = await Users.AddUserIfNotExist(update.Message.Chat.Id, update.Message.From?.Username ?? "");
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