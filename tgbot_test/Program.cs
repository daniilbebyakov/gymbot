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
        var context = new GymBotContext();
        context.Database.EnsureCreated(); // костыль с бд
        var userRep=new UserRepository(context); // автоформатирование
        var interact = new Interact(userRep);
        Host gymbot = new Host(Hidden.token, userRep, interact); // зачем разный тип данных либо вар либо везде прописывать. 
        gymbot.Start();
        Console.ReadLine(); 
    } 
}
