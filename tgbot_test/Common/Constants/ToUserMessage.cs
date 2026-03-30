namespace GymBot.Common.Constants
{
    public static class ToUserMessage
    {
        public const string RegistrationSuccess = "Вы успешно зарегистрированы!";
        public const string UserInfo = "Ваш ID: {0}\nВаш ник: {1}";
        public const string StartMenuPrompt = "Выберите действие: можно сразу нажать кнопку ниже или ввести /addworkout.";
        public const string UnknownCommandHint = "Не понял команду. Доступно: /start, /me, /addworkout.";
        public const string SessionExpired = "Сессия добавления тренировки не найдена. Начните заново: /addworkout.";
        public const string UnknownStatePrompt = "Неожиданное состояние. Попробуйте начать заново командой /addworkout.";

        public const string FlowCanceled = "Добавление тренировки отменено.";
        public const string BackToMainMenu = "Возврат в главное меню.";
        public const string WorkoutSaveError = "Не удалось сохранить тренировку в БД. Попробуйте ещё раз.";

        public const string DatePrompt = "Выберите дату тренировки:";
        public const string CustomDatePrompt = "Введите дату в формате ДД.ММ.ГГГГ (например, 21.03.2026).";
        public const string InvalidDatePrompt = "Некорректная дата. Введите в формате ДД.ММ.ГГГГ.";

        public const string WorkoutTypePrompt = "Выберите тип тренировки: A/B/C или свой шаблон.";
        public const string CustomWorkoutTemplatePrompt = "Введите название своего шаблона тренировки (например, Ноги+Пресс).";

        public const string ExercisePrompt = "Выберите упражнение из списка кнопок или создайте своё.";
        public const string CustomExercisePrompt = "Введите название своего упражнения.";

        public static string WeightPromptForExercise(string exerciseName) => $"Введите вес для упражнения \"{exerciseName}\" в кг (например, 80 или 80.5).";
        public const string InvalidWeightPrompt = "Некорректный вес. Введите число больше 0.";

        public const string RepsPrompt = "Введите количество повторений.";
        public const string InvalidRepsPrompt = "Некорректное количество повторений. Введите целое число больше 0.";

        public const string SetsPrompt = "Введите количество подходов.";
        public const string InvalidSetsPrompt = "Некорректное количество подходов. Введите целое число больше 0.";

        public static string ExerciseSavedPrompt(int exercisesCount) =>
            $"Упражнение сохранено. В тренировке уже {exercisesCount} шт. Добавить ещё или сохранить тренировку?";

        public const string WorkoutCannotSaveWithoutExercises = "Нельзя сохранить пустую тренировку. Добавьте хотя бы одно упражнение.";
    }
}
