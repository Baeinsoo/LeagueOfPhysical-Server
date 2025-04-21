using GameFramework;
using System;
using UnityEngine;

namespace LOP
{
    public static partial class Extensions
    {
        public static void RegisterHandler<T>(this IRoomNetwork network, Action<int, T> handler, IMessageInterceptorWithId interceptor = null) where T : IMessage
        {
            network.RegisterHandler<T>((id, message) =>
            {
                try
                {
                    interceptor?.OnBeforeHandle(id, message);
                    handler(id, message);
                    interceptor?.OnAfterHandle(id, message);
                }
                catch (Exception e)
                {
                    interceptor?.OnError(id, message, e.Message);
                }
            });
        }
    }
}
