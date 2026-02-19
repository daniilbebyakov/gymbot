namespace GymBot.Common.Constants
{
    public static class ToUserMessage
    {
        public const string RegistrationSuccess = "Вы успешно зарегистрированы!";
        public const string UserInfo = "Ваш ID: {0}\nВаш ник: {1}";
        public const string WorkoutCreated = "Тренировка \"{0}\" создана. ID тренивроки: {1}";
        public const string ExerciseAdded = "Упражнение \"{0}\" добавлено в тренировку {1}: {2}x{3}.";
        public const string CommandFormatWorkout = "Формат: /workout <название тренировки>";
        public const string CommandFormatExercise = "Формат: /exercise <ID тренировки> <название>";
        public const string CommandFormatSet = "Формат: /set <ID упражнения> <подходы> <вес>";
    }
}
