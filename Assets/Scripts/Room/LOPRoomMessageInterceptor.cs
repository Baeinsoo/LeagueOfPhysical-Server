using GameFramework;
using System;
using UnityEngine;

namespace LOP
{
    public class LOPRoomMessageInterceptor : IMessageInterceptorWithId
    {
        public static readonly LOPRoomMessageInterceptor Default = new LOPRoomMessageInterceptor();

        public void OnBeforeHandle<T>(int id, T message) where T : IMessage
        {
            var dataContextManager = SceneLifetimeScope.Resolve<IDataContextManager>();

            dataContextManager.UpdateData(message);
        }

        public void OnAfterHandle<T>(int id, T message) where T : IMessage { }

        public void OnError<T>(int id, T message, string error) where T : IMessage
        {
            Debug.LogError($"error: {error}");
        }
    }
}
