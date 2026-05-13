using GymBot.Common.Constants;
using GymBot.Data;
using GymBot.Data.Data.Repositories;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using System.Globalization;
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
        private readonly TemplateRepository _template;
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
            ExerciseSaved,
            CreatingTemplateName,
            CreatingTemplateExercise,
            CreatingTemplateConfirm
        }
        private sealed record WorkoutExerciseInput(string Name, decimal Weight, int Reps, int Sets);
        private sealed class AddWorkoutSession
        {
            public AddWorkoutStep Step { get; set; }
            public DateOnly WorkoutDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
            public string WorkoutTemplate { get; set; } = string.Empty;
            public string? CurrentExerciseName { get; set; } = null;
            public decimal CurrentWeight { get; set; }
            public int CurrentReps { get; set; }
            public List<WorkoutExerciseInput> Exercises { get; } = [];
            public bool IsTemplateCreationMode { get; set; }
            public string? NewTemplateName { get; set; }
            public List<string> NewTemplateExercises { get; } = new();
            public List<string> PreferredExercises { get; } = [];
        }

        public Interact(UserRepository user, WorkoutRepository workout, TemplateRepository template)
        {
            _user = user;
            _workout = workout;
            _template = template;
        }
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
                case (AddWorkout):
                    await StartAddWorkoutFlow(client, chatId);
                    break;
                case (WorkoutHistory):
                    await _workout.GetWorkoutHistory(chatId, 0);
                    await client.SendMessage(chatId, WorkoutHistoryTitle, replyMarkup: await BuildWorkoutHistoryKeyboard(chatId, 0));
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

            if (data == "start:view_history")
            {
                var wcount=await _workout.GetWorkoutHistoryCount(chatId);
                if (wcount < 1)
                {
                    await client.SendMessage(chatId, WorkoutHistoryEmpty, replyMarkup: BuildStartKeyboard());
                    return;
                }
                    await client.SendMessage(chatId, WorkoutHistoryTitle, replyMarkup: await BuildWorkoutHistoryKeyboard(chatId, 0));
                return;
            }
            if (data == "start:progress_report")
            {
                await BuildAndSendProgressReport(client, chatId);
                return;
            }
            if (data.StartsWith("history:page:"))
            {
                if (int.TryParse(data.Replace("history:page:", string.Empty), out var page))
                {
                    await _workout.GetWorkoutHistory(chatId, page);
                    await client.SendMessage(chatId, WorkoutHistoryTitle, replyMarkup: await BuildWorkoutHistoryKeyboard(chatId, page));
                }
                return;
            }

            if (data == "template:create")
            {
                if (!_sessions.TryGetValue(chatId, out var session))
                {
                    await client.SendMessage(chatId, SessionExpired, replyMarkup: BuildStartKeyboard());
                    return;
                }
                session.IsTemplateCreationMode = true;
                session.NewTemplateName = null;
                session.NewTemplateExercises.Clear();
                session.Step = AddWorkoutStep.CreatingTemplateName;
                await client.SendMessage(chatId, CustomWorkoutTemplatePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                return;
            }
            if (data == "template:add_exercise")
            {
                if (!_sessions.TryGetValue(chatId, out var session))
                {
                    await client.SendMessage(chatId, SessionExpired, replyMarkup: BuildStartKeyboard());
                    return;
                }
                session.Step = AddWorkoutStep.CreatingTemplateExercise;
                await client.SendMessage(chatId, CustomExercisePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
            }
            if (data == "template:save")
            {
                if (!_sessions.TryGetValue(chatId, out var session))
                {
                    await client.SendMessage(chatId, SessionExpired, replyMarkup: BuildStartKeyboard());
                    return;
                }
                if (string.IsNullOrWhiteSpace(session.NewTemplateName) || session.NewTemplateExercises.Count == 0)
                {
                    await client.SendMessage(chatId, WorkoutTemplateCannotSave);
                    return;
                }
                var templateId = await _template.CreateTemplate(
                    chatId, session.NewTemplateName, session.NewTemplateExercises);
                session.IsTemplateCreationMode = false;
                session.Step = AddWorkoutStep.ChooseWorkoutTemplate;
                await client.SendMessage(chatId, WorkoutTemplateSaved, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                return;
            }
            if (data.StartsWith("history:delete:"))
            {
                if (!long.TryParse(data.Replace("history:delete:", string.Empty), out var workoutId))
                {
                    await client.SendMessage(chatId, WorkoutDeleteError, replyMarkup: await BuildWorkoutHistoryKeyboard(chatId, 0));
                    return;
                }
                await client.SendMessage(
                    chatId,
                    WorkoutTemplateDeleteConfirm,
                    replyMarkup: BuildWorkoutDeleteConfirmKeyboard(workoutId));
                return;
            }
            if (data.StartsWith("workout:delete_confirm:"))
            {
                if (!long.TryParse(data.Replace("workout:delete_confirm:", string.Empty), out var workoutId))
                {
                    await client.SendMessage(chatId, WorkoutDeleteError, replyMarkup: await BuildWorkoutHistoryKeyboard(chatId, 0));
                    return;
                }
                try
                {
                    await _workout.DeleteWorkout(chatId, workoutId);
                    await client.SendMessage(chatId, WorkoutDeleted, replyMarkup: await BuildWorkoutHistoryKeyboard(chatId, 0));
                }
                catch
                {
                    await client.SendMessage(chatId, WorkoutDeleteError, replyMarkup: await BuildWorkoutHistoryKeyboard(chatId, 0));
                }
            }
            if (data == "workout:delete_cancel")
            {
                await client.SendMessage(chatId, WorkoutDeletedCanceled, replyMarkup: await BuildWorkoutHistoryKeyboard(chatId, 0));
                return;
            }
            if (data.StartsWith("template:delete:"))
            {
                if (!long.TryParse(data.Replace("template:delete:", string.Empty), out var templateId))
                {
                    await client.SendMessage(chatId, UnknownStatePrompt, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                    return;
                }

                var templateToDelete = await _template.GetTemplate(templateId, chatId);
                if (templateToDelete == null)
                {
                    await client.SendMessage(chatId, UnknownStatePrompt, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                    return;
                }

                await client.SendMessage(
                    chatId,
                    string.Format(WorkoutTemplateDeleteConfirm, templateToDelete.Name),
                    replyMarkup: BuildTemplateDeleteConfirmKeyboard(templateId));
                return;
            }
            if (data.StartsWith("template:delete_confirm:"))
            {
                if (!long.TryParse(data.Replace("template:delete_confirm:", string.Empty), out var templateId))
                {
                    await client.SendMessage(chatId, UnknownStatePrompt, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                    return;
                }

                try
                {
                    await _template.DeleteTemplate(templateId, chatId);
                    await client.SendMessage(chatId, WorkoutTemplateDeleted, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                }
                catch
                {
                    await client.SendMessage(chatId, WorkoutTemplateDeleteCancel, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                }
                return;
            }
            if (data == "template:delete_cancel")
            {
                await client.SendMessage(chatId, WorkoutTemplateDeleteCancel, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                return;
            }
            if (data == "nav:cancel")
            {
                _sessions.Remove(chatId);
                await client.SendMessage(chatId, BackToMainMenu, replyMarkup: BuildStartKeyboard());
                return;
            }
            if (data == "nav:back")
            {
                if (!_sessions.TryGetValue(chatId, out var session))
                {
                    await client.SendMessage(chatId, SessionExpired, replyMarkup: BuildStartKeyboard());
                    return;
                }
                await HandleBack(client, chatId, session);
                return;
            }
            if (data == "date:today")
            {
                if (!_sessions.TryGetValue(chatId, out var session))
                {
                    await client.SendMessage(chatId, SessionExpired, replyMarkup: BuildStartKeyboard());
                    return;
                }
                session.WorkoutDate = DateOnly.FromDateTime(DateTime.Today);
                session.Step = AddWorkoutStep.ChooseWorkoutTemplate;
                await client.SendMessage(chatId, WorkoutTypePrompt, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                return;
            }
            if (data == "date:custom")
            {
                if (!_sessions.TryGetValue(chatId, out var session))
                {
                    await client.SendMessage(chatId, SessionExpired, replyMarkup: BuildStartKeyboard());
                    return;
                }
                session.Step = AddWorkoutStep.WaitingCustomDate;
                await client.SendMessage(chatId, CustomDatePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                return;
            }
            if (data.StartsWith("wtemplate:"))
            {
                if (!_sessions.TryGetValue(chatId, out var session))
                {
                    await client.SendMessage(chatId, SessionExpired, replyMarkup: BuildStartKeyboard());
                    return;
                }
                if (!long.TryParse(data.Replace("wtemplate:", string.Empty), out var templateId))
                {
                    await client.SendMessage(chatId, UnknownStatePrompt, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                    return;
                }
                var selectedTemplate = await _template.GetTemplate(templateId, chatId);
                if (selectedTemplate == null)
                {
                    await client.SendMessage(chatId, UnknownStatePrompt, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                    return;
                }
                session.WorkoutTemplate = selectedTemplate.Name;
                session.PreferredExercises.Clear();
                session.PreferredExercises.AddRange(selectedTemplate.Exercises.Select(e => e.ExerciseName));
                session.Step = AddWorkoutStep.ChooseExercise;
                await client.SendMessage(chatId,
                    ExercisePrompt,
                    replyMarkup: BuildExerciseKeyboard(session.PreferredExercises));
                return;
            }
            if (data == "exercise:add_more")
            {
                if (!_sessions.TryGetValue(chatId, out var session))
                {
                    await client.SendMessage(chatId, SessionExpired, replyMarkup: BuildStartKeyboard());
                    return;
                }
                session.CurrentExerciseName = null;
                session.CurrentWeight = 0;
                session.CurrentReps = 0;
                session.Step = AddWorkoutStep.ChooseExercise;
                await client.SendMessage(chatId, ExercisePrompt, replyMarkup: BuildExerciseKeyboard(session.PreferredExercises));
                return;
            }
            if (data.StartsWith("exercise:"))
            {
                if (!_sessions.TryGetValue(chatId, out var session))
                {
                    await client.SendMessage(chatId, SessionExpired, replyMarkup: BuildStartKeyboard());
                    return;
                }
                string exercise = data.Replace("exercise:", string.Empty);
                session.CurrentExerciseName = exercise;
                session.Step = AddWorkoutStep.WaitingWeight;
                await client.SendMessage(chatId, WeightPromptForExercise(session.CurrentExerciseName), replyMarkup: BuildNavigationOnlyKeyboard());
                return;
            }
            if (data == "workout:save")
            {
                if (!_sessions.TryGetValue(chatId, out var session))
                {
                    await client.SendMessage(chatId, SessionExpired, replyMarkup: BuildStartKeyboard());
                    return;
                }
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
                    await client.SendMessage(chatId, WorkoutTypePrompt, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                    break;
                case AddWorkoutStep.CreatingTemplateName:
                    session.Step = AddWorkoutStep.ChooseWorkoutTemplate;
                    session.NewTemplateName = null;
                    session.NewTemplateExercises.Clear();
                    await client.SendMessage(chatId, WorkoutTypePrompt, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                    break;
                case AddWorkoutStep.CreatingTemplateExercise:
                    session.Step = AddWorkoutStep.ChooseWorkoutTemplate;
                    await client.SendMessage(chatId, WorkoutTypePrompt, replyMarkup: BuildTemplateDraftKeyboard());
                    break;
                case AddWorkoutStep.ChooseExercise:
                    session.Step = AddWorkoutStep.ChooseWorkoutTemplate;
                    await client.SendMessage(chatId, WorkoutTypePrompt, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                    break;

                case AddWorkoutStep.WaitingCustomExercise:
                    session.Step = AddWorkoutStep.ChooseExercise;
                    await client.SendMessage(chatId, ExercisePrompt, replyMarkup: BuildExerciseKeyboard(session.PreferredExercises));
                    break;

                case AddWorkoutStep.WaitingWeight:
                    session.Step = AddWorkoutStep.ChooseExercise;
                    session.CurrentExerciseName = string.Empty;
                    await client.SendMessage(chatId, ExercisePrompt, replyMarkup: BuildExerciseKeyboard(session.PreferredExercises));
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
                    await client.SendMessage(chatId, ExercisePrompt, replyMarkup: BuildExerciseKeyboard(session.PreferredExercises));
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
                    await client.SendMessage(chatId, WorkoutTypePrompt, replyMarkup: await BuildWorkoutTypeKeyboard(chatId));
                    break;

                case AddWorkoutStep.WaitingCustomWorkoutTemplate:
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        await client.SendMessage(chatId, CustomWorkoutTemplatePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                        return;
                    }
                    session.WorkoutTemplate = text.Trim();
                    session.Step = AddWorkoutStep.ChooseExercise;
                    await client.SendMessage(chatId, ExercisePrompt, replyMarkup: BuildExerciseKeyboard(session.PreferredExercises));
                    break;
                case AddWorkoutStep.CreatingTemplateName:
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        await client.SendMessage(chatId, CustomWorkoutTemplatePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                        return;
                    }
                    session.NewTemplateName = text.Trim();
                    session.NewTemplateExercises.Clear();
                    session.Step = AddWorkoutStep.CreatingTemplateExercise;
                    await client.SendMessage(chatId, CustomExercisePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                    break;
                case AddWorkoutStep.CreatingTemplateExercise:
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        await client.SendMessage(chatId, CustomExercisePrompt, replyMarkup: BuildNavigationOnlyKeyboard());
                        return;
                    }
                    session.NewTemplateExercises.Add(text.Trim());
                    await client.SendMessage(chatId, ExerciseAddedPrompt, replyMarkup: BuildTemplateDraftKeyboard());
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
            [InlineKeyboardButton.WithCallbackData("➕ Добавить тренировку", "start:add_workout")],
            [InlineKeyboardButton.WithCallbackData("📚 История тренировок", "start:view_history")],
            [InlineKeyboardButton.WithCallbackData("📈 Сформировать график прогресса", "start:progress_report")]
        ]);
        private async Task<InlineKeyboardMarkup> BuildWorkoutHistoryKeyboard(long chatId, int currentPage)
        {
            var workouts = await _workout.GetWorkoutHistory(chatId, currentPage);
            var workoutcount = await _workout.GetWorkoutHistoryCount(chatId);
            var totalPages = (int)Math.Ceiling((double)workoutcount / 5);
            var rows = workouts
                .Select(w => new[]
                { InlineKeyboardButton.WithCallbackData($"{w.Date:dd.MM}, Шаблон: {w.Name}",$"whistory:{w.Id}"),
                    InlineKeyboardButton.WithCallbackData("🗑 Удалить", $"history:delete:{w.Id}")
                })
                .ToList();
            var navigationRow = new List<InlineKeyboardButton>();
            if (currentPage > 0)
            {
                navigationRow.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"history:page:{currentPage - 1}"));
            }
            if (currentPage + 1 < totalPages)
            {
                navigationRow.Add(InlineKeyboardButton.WithCallbackData("➡️", $"history:page:{currentPage + 1}"));
            }
            if (navigationRow.Count > 0)
            {
                rows.Add(navigationRow.ToArray());
            }

            rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ В меню", "nav:cancel")]);
            return new InlineKeyboardMarkup(rows);
        }
        private static InlineKeyboardMarkup BuildDateKeyboard() => new(
        [
            [InlineKeyboardButton.WithCallbackData("📌 Сегодня", "date:today")],
            [InlineKeyboardButton.WithCallbackData("📅 Другая дата", "date:custom")],
            .. BuildNavigationRow()
        ]);
        private async Task<InlineKeyboardMarkup> BuildWorkoutTypeKeyboard(long chatId)
        {
            var customTemplates = await _template.GetUserTemplates(chatId);
            var rows = customTemplates
                .Select(x => new[] { InlineKeyboardButton.WithCallbackData($"📌 {x.Name}",$"wtemplate:{x.Id}"),
                InlineKeyboardButton.WithCallbackData("🗑 Удалить", $"template:delete:{x.Id}")})
                .ToList();
            rows.Add([InlineKeyboardButton.WithCallbackData("➕ Создать шаблон", "template:create")]);
            rows.AddRange(BuildNavigationRow());
            return new InlineKeyboardMarkup(rows);
        }
        private static InlineKeyboardMarkup BuildTemplateDeleteConfirmKeyboard(long templateId) => new(
        [
            [InlineKeyboardButton.WithCallbackData("✅ Да, удалить", $"template:delete_confirm:{templateId}")],
            [InlineKeyboardButton.WithCallbackData("↩️ Нет, оставить", "template:delete_cancel")]
        ]);
        private static InlineKeyboardMarkup BuildWorkoutDeleteConfirmKeyboard(long workoutId) => new(
        [
            [InlineKeyboardButton.WithCallbackData("✅ Да, удалить", $"workout:delete_confirm:{workoutId}")],
            [InlineKeyboardButton.WithCallbackData("↩️ Нет, оставить", "workout:delete_cancel")]
        ]);
        private static InlineKeyboardMarkup BuildExerciseKeyboard(IReadOnlyCollection<string> preferredExercises)
        {
            var rows = new List<InlineKeyboardButton[]>();
            foreach (var exercise in preferredExercises)
            {
                rows.Add([InlineKeyboardButton.WithCallbackData($"⭐ {exercise}", $"exercise:{exercise}")]);
            }
            rows.AddRange(BuildNavigationRow());
            return new InlineKeyboardMarkup(rows);
        }
        private static InlineKeyboardMarkup BuildExerciseActionsKeyboard() => new(
    [
        [InlineKeyboardButton.WithCallbackData("➕ Добавить ещё упражнение", "exercise:add_more")],
            [InlineKeyboardButton.WithCallbackData("💾 Сохранить тренировку", "workout:save")],
            .. BuildNavigationRow()
    ]);
        private static InlineKeyboardMarkup BuildTemplateDraftKeyboard() => new(
       [
           [InlineKeyboardButton.WithCallbackData("➕ Добавить упражнение", "template:add_exercise")],
            [InlineKeyboardButton.WithCallbackData("💾 Сохранить шаблон", "template:save")],
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
                .Select((x, i) => $"{i + 1}. {x.Name} — {x.Weight} кг × {x.Reps} повторений × {x.Sets} подхода")
                .ToList();

            return string.Join('\n', [header, "", "Упражнения:", .. lines]);
        }
        private async Task BuildAndSendProgressReport(ITelegramBotClient client, long chatId)
        {
            // 1) Получаем отчет из репозитория
            var report = await _workout.BuildWeightProgressReport(chatId);

            // 2) Отправляем как .xlsx документ в Telegram
            await using var stream = new MemoryStream(report);

            await client.SendDocument(
                chatId: chatId,
                document: InputFile.FromStream(stream, "график прогресса.xlsx"),
                caption: "График прогресса веса за весь период"
            );
            //try
            //{
            //    await client.SendMessage(chatId, ProgressReportBuilding, replyMarkup: BuildStartKeyboard());

            //    var fileBytes = await WorkoutRepository.BuildWeightProgressReport(chatId);

            //    var workoutCount = await _workout.GetWorkoutHistoryCount(chatId);
            //    if (workoutCount <= 0)
            //    {
            //        await client.SendMessage(chatId, ProgressReportEmpty, replyMarkup: BuildStartKeyboard());
            //        return;
            //    }

            //    var allWorkouts = new List<dynamic>();
            //    var totalPages = (int)Math.Ceiling((double)workoutCount / 5);
            //    for (var page = 0; page < totalPages; page++)
            //    {
            //        var pageItems = await _workout.GetWorkoutHistory(chatId, page);
            //        allWorkouts.AddRange(pageItems.Cast<dynamic>());
            //    }

            //    var pointsByExercise = new Dictionary<string, List<(DateTime Date, decimal Weight)>>();

            //    foreach (var workout in allWorkouts)
            //    {
            //        DateTime workoutDate = ((DateOnly)workout.Date).ToDateTime(TimeOnly.MinValue);
            //        foreach (var exercise in workout.Exercises)
            //        {
            //            string exerciseName = exercise.Name;
            //            decimal weight = exercise.Weight;

            //            if (!pointsByExercise.TryGetValue(exerciseName, out var list))
            //            {
            //                list = [];
            //                pointsByExercise[exerciseName] = list;
            //            }

            //            list.Add((workoutDate, weight));
            //        }
            //    }

            //    if (pointsByExercise.Count == 0)
            //    {
            //        await client.SendMessage(chatId, ProgressReportEmpty, replyMarkup: BuildStartKeyboard());
            //        return;
            //    }

            //    ExcelPackage.License.SetNonCommercialPersonal("Danya");
            //    string tempFile = Path.Combine(Path.GetTempPath(), $"weight-progress-{chatId}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");

            //    using (var package = new ExcelPackage())
            //    {
            //        foreach (var pair in pointsByExercise.OrderBy(x => x.Key))
            //        {
            //            var sheetName = pair.Key.Length > 31 ? pair.Key[..31] : pair.Key;
            //            var sheet = package.Workbook.Worksheets.Add(sheetName);

            //            sheet.Cells[1, 1].Value = "Дата";
            //            sheet.Cells[1, 2].Value = "Вес (кг)";

            //            var ordered = pair.Value.OrderBy(x => x.Date).ToList();
            //            for (int i = 0; i < ordered.Count; i++)
            //            {
            //                sheet.Cells[i + 2, 1].Value = ordered[i].Date;
            //                sheet.Cells[i + 2, 1].Style.Numberformat.Format = "dd.MM.yyyy";
            //                sheet.Cells[i + 2, 2].Value = ordered[i].Weight;
            //            }

            //            var chart = sheet.Drawings.AddChart($"chart_{sheetName}", eChartType.LineMarkers) as ExcelLineChart;
            //            chart!.Title.Text = $"Прогресс веса: {pair.Key}";
            //            chart.SetPosition(0, 0, 3, 0);
            //            chart.SetSize(900, 360);
            //            var series = chart.Series.Add(sheet.Cells[2, 2, ordered.Count + 2 - 1, 2], sheet.Cells[2, 1, ordered.Count + 2 - 1, 1]);
            //            series.Header = "Вес";
            //            chart.YAxis.Title.Text = "Вес (кг)";
            //            chart.XAxis.Title.Text = "Дата";

            //            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
            //        }

            //        await package.SaveAsAsync(new FileInfo(tempFile));
            //    }

            //    await using var stream = File.OpenRead(tempFile);
            //    await client.SendDocument(chatId, InputFile.FromStream(stream, "weight-progress-report.xlsx"));
            //    await client.SendMessage(chatId, ProgressReportReady, replyMarkup: BuildStartKeyboard());
            //    File.Delete(tempFile);
            //}
            //catch
            //{
            //    await client.SendMessage(chatId, ProgressReportError, replyMarkup: BuildStartKeyboard());
            //}
        }
    }
}
