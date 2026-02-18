using GymBot;
using GymBot.Data;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    private static void Main(string[] args)
    {
        var context = new GymBotContext();
        context.Database.Migrate();
        var userRep = new UserRepository(context);
        var interact = new Interact(userRep);
        Host gymbot = new Host(Hidden.token, userRep, interact);
        gymbot.Start();
        Console.ReadLine();
    }
}