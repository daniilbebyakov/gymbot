namespace GymBot.Common.Constants
{
    public static class ToUserMessage
    {
        public const string RegistrationSuccess = "Вы успешно зарегистрированы!";
        public const string UserInfo = "Ваш ID: {0}\nВаш ник: {1}";
        public const string StartMenuPrompt = "Выберите действие:";
        public const string UnknownCommandHint = "Не понял команду. Доступно: /start, /me, /addworkout, /workouthistory.";
        public const string SessionExpired = "Сессия добавления тренировки не найдена. Начните заново: /addworkout.";
        public const string UnknownStatePrompt = "Неожиданное состояние. Попробуйте начать заново командой /addworkout.";

        public const string BackToMainMenu = "Возврат в главное меню.";
        public const string WorkoutSaveError = "Не удалось сохранить тренировку в БД. Попробуйте ещё раз.";

        public const string DatePrompt = "Выберите дату тренировки:";
        public const string CustomDatePrompt = "Введите дату в формате ДД.ММ.ГГГГ (например, 21.03.2026).";
        public const string InvalidDatePrompt = "Некорректная дата. Введите в формате ДД.ММ.ГГГГ.";

        public const string WorkoutTypePrompt = "Выберите шаблон тренировки.";
        public const string CustomWorkoutTemplatePrompt = "Введите название шаблона (например, Ноги+Пресс).";

        public const string ExercisePrompt = "Выберите упражнение из списка.";
        public const string CustomExercisePrompt = "Введите название упражнения.";
        public const string ExerciseAddedPrompt = "Упражнение добавлено";

        public const string WorkoutTemplateCannotSave = "Введите название и минимум одно упражнение";
        public const string WorkoutTemplateSaved = "Шаблон сохранён";
        public const string WorkoutTemplateDeleteConfirm = "Вы действительно хотите удалить шаблон?";
        public const string WorkoutTemplateDeleted = "Шаблон успешно удален";
        public const string WorkoutTemplateDeleteCancel = "Отмена удаления шаблона";

        public static string WeightPromptForExercise(string exerciseName) => $"Введите вес для упражнения \"{exerciseName}\" в кг (например, 80 или 80.5).";
        public const string InvalidWeightPrompt = "Некорректный вес. Введите число больше 0.";

        public const string RepsPrompt = "Введите количество повторений.";
        public const string InvalidRepsPrompt = "Некорректное количество повторений. Введите целое число больше 0.";

        public const string SetsPrompt = "Введите количество подходов.";
        public const string InvalidSetsPrompt = "Некорректное количество подходов. Введите целое число больше 0.";

        public static string ExerciseSavedPrompt(int exercisesCount) =>
            $"Упражнение сохранено. В тренировке уже {exercisesCount} шт. Добавить ещё или сохранить тренировку?";

        public const string WorkoutCannotSaveWithoutExercises = "Нельзя сохранить пустую тренировку. Добавьте хотя бы одно упражнение.";

        public const string WorkoutHistoryEmpty = "История тренировок пока пустая.";
        public const string WorkoutHistoryTitle = "История тренировок";
        public const string WorkoutHistoryLoadError = "Не удалось загрузить историю тренировок. Попробуйте позже.";
        public const string WorkoutDeleted = "Тренировка удалена.";
        public const string WorkoutDeleteError = "Не удалось удалить тренировку.";
        public const string WorkoutDeleteConfirm = "Вы действительно хотите удалить тренировку?";
        public const string WorkoutDeletedCanceled = "Удаление тренировки отменено.";
    }
}
