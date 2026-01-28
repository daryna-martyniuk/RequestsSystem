namespace Requests.Services
{
    public static class ServiceConstants
    {
        // === СТАТУСИ ЗАПИТІВ (Global) ===
        public const string StatusNew = "Новий";
        public const string StatusPendingApproval = "Очікує погодження";
        public const string StatusClarification = "На уточненні";
        public const string StatusInProgress = "В роботі";
        public const string StatusRejected = "Відхилено";
        public const string StatusCanceled = "Скасовано";
        public const string StatusCompleted = "Завершено";

        // === СТАТУСИ ЗАДАЧ (Local) ===
        public const string TaskStatusNew = "Новий";
        public const string TaskStatusInProgress = "В роботі";
        public const string TaskStatusPaused = "На паузі";
        public const string TaskStatusDone = "Виконано";

        // === ПРІОРИТЕТИ ===
        public const string PriorityLow = "Низький";
        public const string PriorityNormal = "Середній";
        public const string PriorityHigh = "Високий";
        public const string PriorityCritical = "Критичний";

        // === РОЛІ (ПОСАДИ) ===
        public const string PositionAdmin = "Адміністратор";
        public const string PositionDirector = "Директор";
        public const string PositionDeputyDirector = "Заступник директора";
        public const string PositionHead = "Керівник відділу";
        public const string PositionDeputyHead = "Заступник керівника";
        public const string PositionEmployee = "Співробітник";
    }
}