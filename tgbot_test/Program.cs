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
        Host gymbot = new Host(Hidden.token);
        gymbot.Start();
        gymbot.OnMessage += Interact.OnMessage;
        Console.ReadLine(); 
    } 
}