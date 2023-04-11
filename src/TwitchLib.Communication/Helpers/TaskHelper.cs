using System.Threading.Tasks;

namespace TwitchLib.Communication.Helpers
{
    internal static class TaskHelper
    {
        internal static bool IsTaskRunning(this Task task)
        {
            return task != null
                   && !task.IsFaulted
                   && !task.IsCompleted
#if NET
            && !task.IsCompletedSuccessfully
#endif
                   && !task.IsCanceled;
        }
    }
}