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
    string connection = "Host=localhost;Port=5432;Username=gymbotuser;Password=gymbotpass;Database=gymbot_db";

    static UserRepository Users = new UserRepository();
    private static async void OnMessage(ITelegramBotClient client, Update update)
    {
        if (update.Message == null) return;
        await Users.AddUser(update.Message.Chat.Id, update.Message.From?.Username);
        await client.SendMessage(update.Message.Chat.Id, "Теперь ты в файлах Эйпштена, пидар.");
        await client.SendMessage(update.Message?.Chat.Id ?? 445584914, update.Message?.Text?? "[не текст]");
    }
}