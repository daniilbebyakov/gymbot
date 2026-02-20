using GymBot.Common.Constants;
using GymBot.Data;
using GymBot.Data.Data.Repositories;
using System.Globalization; //?
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using static GymBot.Common.Constants.BotCommands;
using static GymBot.Common.Constants.BotMessages;
using static GymBot.Common.Constants.ToUserMessage;
using static System.Collections.Specialized.BitVector32;

namespace GymBot
{
    public class Interact
    {
        private readonly UserRepository _user;
        private readonly Dictionary<long, AddWorkoutSession> _sessions = new();
        private enum AddWorkoutStep
        {
            ChooseDate,
            WaitingCustomDate,
            ChooseWorkoutTemplate,
            WaitingCustomWorkoutTemplate,
            ChooseExercise,
            WaitingCustomExercise,
            WaitingWeight,
            WaitingReps,
            WaitingSets,
            ExerciseSaved
        }
        private sealed record WorkoutExerciseInput(string Name, decimal Weight, int Reps, int Sets);
        private sealed class AddWorkoutSession
        {
            public AddWorkoutStep Step { get; set; }
            public DateOnly WorkoutDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
            public string WorkoutTemplate { get; set; } = "A";
            public string CurrentExerciseName { get; set; } = string.Empty;
            public decimal CurrentWeight { get; set; }
            public int CurrentReps { get; set; }
            public List<WorkoutExerciseInput> Exercises { get; } = [];
        }

        public Interact(UserRepository user)
        {
            _user = user;
        }

        public async Task OnMessage(ITelegramBotClient client, Update update)
        {
            //if (update.CallbackQuery != null)
            //{
            //    await HandleCallback(client, update.CallbackQuery);
            //    return;
            //}
            if (update.Message == null) return;
            string usermessage = update.Message.Text ?? String.Empty;
            long chatId = update.Message.Chat.Id;
            switch (usermessage)
            {
                case (Start):
                    bool added = await _user.AddUserIfNotExist(chatId, update.Message.From?.Username ?? String.Empty);
                    if (added)
                    {
                        await client.SendMessage(chatId, ToUserMessage.RegistrationSuccess);
                    }
                    await client.SendMessage(chatId, StartMenuPrompt, replyMarkup: BuildStartKeyboard());
                    break;
                case (Me):
                    await client.SendMessage(chatId, string.Format(ToUserMessage.UserInfo,
                        update.Message.Chat.Id, update.Message.From?.Username ?? "Нет ника"));
                    break;
                case (AddWorkout):
                    await StartAddWorkoutFlow(client, chatId);
                    break;
                default:
                    await client.SendMessage(chatId, update.Message?.Text ?? BotMessages.BotMessageNoText);
                    break;
            }
        }
            private async Task HandleCallback(ITelegramBotClient client, CallbackQuery callback)
        {
            if (callback.Message == null || string.IsNullOrWhiteSpace(callback.Data)) return;
            long chatId = callback.Message.Chat.Id;
            string data = callback.Data;
            await client.AnswerCallbackQuery(callback.Id);
            if (data == "start:add_workout")
            {
                await StartAddWorkoutFlow(client, chatId);
                return;
            }
            if (data == "nav:cancel")
            {
                _sessions.Remove(chatId);
                await client.SendMessage(chatId, FlowCanceled, replyMarkup: BuildStartKeyboard());
                return;
            }
            if (data == "nav:back")
            {
                await HandleBack(client, chatId, session);
                return;
            }
            if (data == "date:today")
            {
                session.WorkoutDate = DateOnly.FromDateTime(DateTime.Today);
                session.Step = AddWorkoutStep.ChooseWorkoutTemplate;
                await client.SendMessage(chatId, WorkoutTypePrompt, replyMarkup: BuildWorkoutTypeKeyboard());
                return;
            }
        }

        private async Task StartAddWorkoutFlow(ITelegramBotClient client, long chatId)
        {
            _sessions[chatId] = new AddWorkoutSession
            {
                Step = AddWorkoutStep.ChooseDate
            };

            await client.SendMessage(chatId, DatePrompt, replyMarkup: BuildDateKeyboard());
        }
        private static InlineKeyboardMarkup BuildStartKeyboard() => new(
        [
            [InlineKeyboardButton.WithCallbackData("➕ Добавить тренировку", "start:add_workout")]
        ]);

    }
}
