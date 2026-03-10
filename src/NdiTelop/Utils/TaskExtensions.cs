using System.Threading.Tasks;

namespace NdiTelop.Utils;

public static class TaskExtensions
{
    public static void FireAndForget(this Task task)
    {
        // エラーハンドリングは別途考慮が必要
        // ここでは単純に例外を無視する
        // TODO: ロギングなどを追加する
    }
}
