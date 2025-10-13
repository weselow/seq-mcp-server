using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace SeqMcp.Prompts;

[McpServerPromptType]
public class SeqPrompts
{
    [McpServerPrompt(Name = "seq_analyze_errors")]
    [Description("Анализ ошибок за период")]
    public ChatMessage AnalyzeErrors(
        [Description("Период: 1h, 24h, 7d")] string period = "1h")
    {
        return new(ChatRole.User,
            $"Используй seq_search_events с фильтром \"Level in ['Error', 'Fatal'] and @Timestamp > Now() - {period}\".\n" +
            "Проанализируй:\n" +
            "1. Топ-5 частых ошибок\n" +
            "2. Критичные паттерны\n" +
            "3. Рекомендации");
    }

    [McpServerPrompt(Name = "seq_top_exceptions")]
    [Description("Топ исключений с группировкой")]
    public ChatMessage TopExceptions(
        [Description("Количество")] int count = 10)
    {
        return new(ChatRole.User,
            "Используй seq_execute_sql:\n" +
            $"\"select @Exception, count(*) as cnt from stream where @Exception is not null group by @Exception order by cnt desc limit {count}\"\n" +
            "Покажи таблицу с анализом каждого исключения");
    }

    [McpServerPrompt(Name = "seq_activity_summary")]
    [Description("Сводка активности по уровням")]
    public ChatMessage ActivitySummary(
        [Description("Период: 1h, 24h, 7d")] string period = "24h")
    {
        return new(ChatRole.User,
            "Используй seq_execute_sql:\n" +
            $"\"select Level, count(*) as cnt from stream where @Timestamp > Now() - {period} group by Level order by cnt desc\"\n" +
            "Создай краткую сводку с процентами");
    }

    [McpServerPrompt(Name = "seq_check_signals")]
    [Description("Проверка активных сигналов")]
    public ChatMessage CheckSignals()
    {
        return new(ChatRole.User,
            "Используй seq_list_signals для получения всех сигналов.\n" +
            "Для каждого сигнала:\n" +
            "1. Запусти его фильтр через seq_search_events (limit 10)\n" +
            "2. Покажи есть ли совпадения\n" +
            "3. Оцени критичность");
    }

    [McpServerPrompt(Name = "seq_performance_check")]
    [Description("Анализ производительности")]
    public ChatMessage PerformanceCheck(
        [Description("Период: 1h, 24h")] string period = "1h")
    {
        return new(ChatRole.User,
            "Используй seq_execute_sql:\n" +
            $"\"select Level, count(*) as cnt from stream where @Timestamp > Now() - {period} group by Level\"\n" +
            "Дополнительно найди:\n" +
            "- События с Elapsed > 1000ms\n" +
            "- Память/CPU проблемы\n" +
            "- Deadlock/timeout");
    }

    [McpServerPrompt(Name = "seq_trace_request")]
    [Description("Трассировка запроса по ID")]
    public ChatMessage TraceRequest(
        [Description("RequestId или CorrelationId")] string requestId)
    {
        return new(ChatRole.User,
            $"Используй seq_search_events с фильтром:\n" +
            $"\"RequestId = '{requestId}' or CorrelationId = '{requestId}' or TraceId = '{requestId}'\"\n" +
            "Покажи:\n" +
            "1. Хронологию событий\n" +
            "2. Путь через сервисы\n" +
            "3. Ошибки/проблемы");
    }

    [McpServerPrompt(Name = "seq_security_audit")]
    [Description("Проверка событий безопасности")]
    public ChatMessage SecurityAudit(
        [Description("Период: 1h, 24h, 7d")] string period = "24h")
    {
        return new(ChatRole.User,
            "Используй seq_search_events для поиска:\n" +
            $"1. \"Level = 'Error' and (@Message like '%auth%' or @Message like '%login%') and @Timestamp > Now() - {period}\"\n" +
            "2. \"@Message like '%unauthorized%' or @Message like '%forbidden%'\"\n" +
            "Анализируй подозрительные паттерны");
    }

    [McpServerPrompt(Name = "seq_daily_report")]
    [Description("Ежедневный отчет о логах")]
    public ChatMessage DailyReport()
    {
        return new(ChatRole.User,
            "Создай отчет за последние 24h:\n" +
            "1. seq_execute_sql: статистика по уровням\n" +
            "2. seq_search_events: топ-5 ошибок\n" +
            "3. seq_list_signals: статус сигналов\n" +
            "4. Исключения и паттерны\n" +
            "Формат: краткая executive summary");
    }
}
