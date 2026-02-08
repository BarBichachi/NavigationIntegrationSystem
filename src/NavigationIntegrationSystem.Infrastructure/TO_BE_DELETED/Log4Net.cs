using NavigationIntegrationSystem.Core.Logging;

using System;

namespace log4net
{
    public interface ILog
    {
        void Info(string message);
        void Error(string message);
        void Error(string message, Exception ex);
    }

    public class Log4NetAdapter : ILog
    {
        private readonly ILogService m_Target;
        private readonly string m_Source;

        public Log4NetAdapter(ILogService target, string source)
        {
            m_Target = target;
            m_Source = source;
        }

        public void Info(string message) => m_Target.Info(m_Source, message);
        public void Error(string message) => m_Target.Error(m_Source, message);
        public void Error(string message, Exception ex) => m_Target.Error(m_Source, message, ex);
    }
}