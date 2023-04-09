using System.Threading.Tasks;

namespace TwitchLib.Communication.Helpers
{
    internal static class TaskHelper
    {
        internal static bool IsTaskRunning(Task task)
        {
            return task != null
                && !task.IsFaulted
                && !task.IsCompleted
#if NET
            && !task.IsCompletedSuccessfully
#endif
                && !task.IsCanceled;

            //if (task == null) return false;
            //switch (task.Status)
            //{
            //    case TaskStatus.RanToCompletion:
            //    case TaskStatus.Faulted:
            //    case TaskStatus.Canceled:
            //        return false;
            //}
            //return true;
        }
    }
}
