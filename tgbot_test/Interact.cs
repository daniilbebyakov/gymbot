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
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Collections.Specialized.BitVector32;

namespace GymBot
{
    public class Interact
    {
        private readonly UserRepository _user;
        private readonly WorkoutRepository _workout;
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
            public string? CurrentExerciseName { get; set; } = null;
            public decimal CurrentWeight { get; set; }
            public int CurrentReps { get; set; }
            public List<WorkoutExerciseInput> Exercises { get; } = [];
        }

        public Interact(UserRepository user, WorkoutRepository workout)
        {
            _user = user;
            _workout = workout;
        }
        private static readonly string[] WorkoutTemplates = ["A", "B", "C"];
        private static readonly string[] ExerciseTemplates =
        [
            "Присед",
            "Жим лежа",
            "Тяга штанги в наклоне",
            "Становая тяга",
            "Жим гантелей сидя",
            "Подтягивания"
        ];
        public async Task OnMessage(ITelegramBotClient client, Telegram.Bot.Types.Update update)
        {
            if (update.CallbackQuery != null)
            {
                await HandleCallback(client, update.CallbackQuery);
                return;
            }
            if (update.Message == null) return;
            string usermessage = update.Message.Text ?? String.Empty;
            long chatId = update.Message.Chat.Id;
            if (_sessions.TryGetValue(chatId, out var session))
            {
                await HandleSessionTextInput(client, chatId, usermessage, session);
                return;
            }
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
            if (!_sessions.TryGetValue(chatId, out var session))
            {
                await client.SendMessage(chatId, SessionExpired, replyMarkup: BuildStartKeyboard());
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
            if (data == "date:custom")
            {
                session.Step = AddWorkoutStep.WaitingCustomDate;
                await client.SendMessage(chatId, CustomDatePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                return;
            }
            if (data.StartsWith("wtemplate:"))
            {
                string template = data.Replace("wtemplate:", string.Empty);
                if (template == "custom")
                {
                    session.Step = AddWorkoutStep.WaitingCustomWorkoutTemplate;
                    await client.SendMessage(chatId, CustomWorkoutTemplatePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                    return;
                }

                session.WorkoutTemplate = template;
                session.Step = AddWorkoutStep.ChooseExercise;
                await client.SendMessage(chatId, ExercisePrompt, replyMarkup: BuildExerciseKeyboard());
                return;
            }
            if (data == "exercise:add_more")
            {
                session.CurrentExerciseName = null;
                session.CurrentWeight = 0;
                session.CurrentReps = 0;
                session.Step = AddWorkoutStep.ChooseExercise;
                await client.SendMessage(chatId, ExercisePrompt, replyMarkup: BuildExerciseKeyboard());
                return;
            }
            if (data.StartsWith("exercise:"))
            {
                string exercise = data.Replace("exercise:", string.Empty);
                if (exercise == "custom")
                {
                    session.Step = AddWorkoutStep.WaitingCustomExercise;
                    await client.SendMessage(chatId, CustomExercisePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                    return;
                }

                session.CurrentExerciseName = exercise;
                session.Step = AddWorkoutStep.WaitingWeight;
                await client.SendMessage(chatId, WeightPromptForExercise(session.CurrentExerciseName), replyMarkup: BuildNavigationOnlyKeyboard());
                return;
            }
            if (data == "workout:save")
            {
                if (session.Exercises.Count == 0)
                {
                    await client.SendMessage(chatId, WorkoutCannotSaveWithoutExercises, replyMarkup: BuildExerciseActionsKeyboard());
                    return;
                }
                try
                {
                    var workoutId = await _workout.SaveWorkout(
                        chatId,
                        session.WorkoutDate,
                        session.WorkoutTemplate,
                        session.Exercises.Select(x => new WorkoutRepository.WorkoutExerciseDto(x.Name, x.Weight, x.Reps, x.Sets)).ToList());

                    string summary = BuildWorkoutSummary(session, workoutId);
                    _sessions.Remove(chatId);
                    await client.SendMessage(chatId, summary, replyMarkup: BuildStartKeyboard());
                }
                catch
                {
                    await client.SendMessage(chatId, WorkoutSaveError, replyMarkup: BuildExerciseActionsKeyboard());
                }
            }
        }
        private async Task HandleBack(ITelegramBotClient client, long chatId, AddWorkoutSession session)
        {
            switch (session.Step)
            {
                case AddWorkoutStep.ChooseDate:
                    _sessions.Remove(chatId);
                    await client.SendMessage(chatId, BackToMainMenu, replyMarkup: BuildStartKeyboard());
                    break;

                case AddWorkoutStep.WaitingCustomDate:
                    session.Step = AddWorkoutStep.ChooseDate;
                    await client.SendMessage(chatId, DatePrompt, replyMarkup: BuildDateKeyboard());
                    break;

                case AddWorkoutStep.ChooseWorkoutTemplate:
                    session.Step = AddWorkoutStep.ChooseDate;
                    await client.SendMessage(chatId, DatePrompt, replyMarkup: BuildDateKeyboard());
                    break;

                case AddWorkoutStep.WaitingCustomWorkoutTemplate:
                    session.Step = AddWorkoutStep.ChooseWorkoutTemplate;
                    await client.SendMessage(chatId, WorkoutTypePrompt, replyMarkup: BuildWorkoutTypeKeyboard());
                    break;

                case AddWorkoutStep.ChooseExercise:
                    session.Step = AddWorkoutStep.ChooseWorkoutTemplate;
                    await client.SendMessage(chatId, WorkoutTypePrompt, replyMarkup: BuildWorkoutTypeKeyboard());
                    break;

                case AddWorkoutStep.WaitingCustomExercise:
                    session.Step = AddWorkoutStep.ChooseExercise;
                    await client.SendMessage(chatId, ExercisePrompt, replyMarkup: BuildExerciseKeyboard());
                    break;

                case AddWorkoutStep.WaitingWeight:
                    session.Step = AddWorkoutStep.ChooseExercise;
                    session.CurrentExerciseName = string.Empty;
                    await client.SendMessage(chatId, ExercisePrompt, replyMarkup: BuildExerciseKeyboard());
                    break;

                case AddWorkoutStep.WaitingReps:
                    session.Step = AddWorkoutStep.WaitingWeight;
                    await client.SendMessage(chatId, WeightPromptForExercise(session.CurrentExerciseName ?? "упражнения"), replyMarkup: BuildNavigationOnlyKeyboard());
                    break;

                case AddWorkoutStep.WaitingSets:
                    session.Step = AddWorkoutStep.WaitingReps;
                    await client.SendMessage(chatId, RepsPrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                    break;

                case AddWorkoutStep.ExerciseSaved:
                    session.Step = AddWorkoutStep.ChooseExercise;
                    await client.SendMessage(chatId, ExercisePrompt, replyMarkup: BuildExerciseKeyboard());
                    break;

                default:
                    await client.SendMessage(chatId, UnknownStatePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                    break;
            }
        }
        private async Task HandleSessionTextInput(ITelegramBotClient client, long chatId, string text, AddWorkoutSession session)
        {
            switch (session.Step)
            {
                case AddWorkoutStep.WaitingCustomDate:
                    if (!DateOnly.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        await client.SendMessage(chatId, InvalidDatePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                        return;
                    }

                    session.WorkoutDate = date;
                    session.Step = AddWorkoutStep.ChooseWorkoutTemplate;
                    await client.SendMessage(chatId, WorkoutTypePrompt, replyMarkup: BuildWorkoutTypeKeyboard());
                    break;

                case AddWorkoutStep.WaitingCustomWorkoutTemplate:
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        await client.SendMessage(chatId, CustomWorkoutTemplatePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                        return;
                    }

                    session.WorkoutTemplate = text.Trim();
                    session.Step = AddWorkoutStep.ChooseExercise;
                    await client.SendMessage(chatId, ExercisePrompt, replyMarkup: BuildExerciseKeyboard());
                    break;

                case AddWorkoutStep.WaitingCustomExercise:
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        await client.SendMessage(chatId, CustomExercisePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                        return;
                    }

                    session.CurrentExerciseName = text.Trim();
                    session.Step = AddWorkoutStep.WaitingWeight;
                    await client.SendMessage(chatId, WeightPromptForExercise(session.CurrentExerciseName), replyMarkup: BuildNavigationOnlyKeyboard());
                    break;

                case AddWorkoutStep.WaitingWeight:
                    if (!decimal.TryParse(text.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal weight) || weight <= 0)
                    {
                        await client.SendMessage(chatId, InvalidWeightPrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                        return;
                    }

                    session.CurrentWeight = weight;
                    session.Step = AddWorkoutStep.WaitingReps;
                    await client.SendMessage(chatId, RepsPrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                    break;

                case AddWorkoutStep.WaitingReps:
                    if (!int.TryParse(text, out int reps) || reps <= 0)
                    {
                        await client.SendMessage(chatId, InvalidRepsPrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                        return;
                    }

                    session.CurrentReps = reps;
                    session.Step = AddWorkoutStep.WaitingSets;
                    await client.SendMessage(chatId, SetsPrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                    break;

                case AddWorkoutStep.WaitingSets:
                    if (!int.TryParse(text, out int sets) || sets <= 0)
                    {
                        await client.SendMessage(chatId, InvalidSetsPrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(session.CurrentExerciseName))
                    {
                        await client.SendMessage(chatId, SessionExpired, replyMarkup: BuildStartKeyboard());
                        _sessions.Remove(chatId);
                        return;
                    }

                    session.Exercises.Add(new WorkoutExerciseInput(
                        session.CurrentExerciseName,
                        session.CurrentWeight,
                        session.CurrentReps,
                        sets));

                    session.CurrentExerciseName = null;
                    session.CurrentWeight = 0;
                    session.CurrentReps = 0;
                    session.Step = AddWorkoutStep.ExerciseSaved;
                    await client.SendMessage(chatId, ExerciseSavedPrompt(session.Exercises.Count), replyMarkup: BuildExerciseActionsKeyboard());
                    break;

                default:
                    await client.SendMessage(chatId, UnknownStatePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                    break;
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
        private static InlineKeyboardMarkup BuildDateKeyboard() => new(
        [
            [InlineKeyboardButton.WithCallbackData("📌 Сегодня", "date:today")],
            [InlineKeyboardButton.WithCallbackData("📅 Другая дата", "date:custom")],
            .. BuildNavigationRow()
        ]);
        private static InlineKeyboardMarkup BuildWorkoutTypeKeyboard()
        {
            var rows = WorkoutTemplates
                .Select(x => new[] { InlineKeyboardButton.WithCallbackData(x, $"wtemplate:{x}") })
                .ToList();

            rows.Add([InlineKeyboardButton.WithCallbackData("📋 Свой шаблон", "wtemplate:custom")]);
            rows.AddRange(BuildNavigationRow());
            return new InlineKeyboardMarkup(rows);
        }
        private static InlineKeyboardMarkup BuildExerciseKeyboard()
        {
            var rows = ExerciseTemplates
                .Select(x => new[] { InlineKeyboardButton.WithCallbackData(x, $"exercise:{x}") })
                .ToList();

            rows.Add([InlineKeyboardButton.WithCallbackData("Своё упражнение", "exercise:custom")]);
            rows.AddRange(BuildNavigationRow());
            return new InlineKeyboardMarkup(rows);
        }
        private static InlineKeyboardMarkup BuildExerciseActionsKeyboard() => new(
    [
        [InlineKeyboardButton.WithCallbackData("➕ Добавить ещё упражнение", "exercise:add_more")],
            [InlineKeyboardButton.WithCallbackData("💾 Сохранить тренировку", "workout:save")],
            .. BuildNavigationRow()
    ]);
        private static InlineKeyboardMarkup BuildNavigationOnlyKeyboard() => new(BuildNavigationRow());
        private static List<InlineKeyboardButton[]> BuildNavigationRow() =>
        [
            [
                InlineKeyboardButton.WithCallbackData("⬅️ Назад", "nav:back"),
                InlineKeyboardButton.WithCallbackData("✖️ Отмена", "nav:cancel")
            ]
        ];
        private static string BuildWorkoutSummary(AddWorkoutSession session, long workoutId)
        {
            var header = $"✅ Тренировка сохранена \nДата: {session.WorkoutDate:dd.MM.yyyy}\nШаблон: {session.WorkoutTemplate}";
            var lines = session.Exercises
                .Select((x, i) => $"{i + 1}. {x.Name} — {x.Weight} кг × {x.Reps} повторений × {x.Sets} подходов")
                .ToList();

            return string.Join('\n', [header, "", "Упражнения:", .. lines]);
        }
    }
}
