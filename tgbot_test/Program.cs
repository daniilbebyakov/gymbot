using GymBot;
using GymBot.Data.Data;
using GymBot.Data.Data.Repositories;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    private static void Main(string[] args)
    {
        var context = new GymBotContext();
        context.Database.Migrate();
        var userRep = new UserRepository(context);
        var workoutRep= new WorkoutRepository(context);
        var interact = new Interact(userRep, workoutRep);
        Host gymbot = new Host(Hidden.token, userRep, interact);
        gymbot.Start();
        Console.ReadLine();
    }
}