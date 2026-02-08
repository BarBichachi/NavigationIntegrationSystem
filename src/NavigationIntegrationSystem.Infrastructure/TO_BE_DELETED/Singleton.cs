namespace Infrastructure.Templates;

// Shim for VIC global singleton pattern
public static class Singleton<T> where T : new()
{
    private static T m_Instance = new T();
    public static T Instance => m_Instance;
}