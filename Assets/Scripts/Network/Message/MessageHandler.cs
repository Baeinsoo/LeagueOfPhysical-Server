using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public abstract class MessageHandlerBase
    {
        public abstract void Invoke(int id, IMessage message);
        public abstract bool IsEmpty { get; }
    }

    public class MessageHandler<T> : MessageHandlerBase where T : IMessage
    {
        private Action<int, T> handlers;

        public override void Invoke(int id, IMessage message)
        {
            if (message is T typedMessage)
            {
                handlers.Invoke(id, typedMessage);
            }
            else
            {
                Debug.LogError($"Invalid message type: Expected {typeof(T)}, but got {message.GetType()}");
            }
        }

        public void AddHandler(Action<int, T> handler)
        {
            handlers += handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void RemoveHandler(Action<int, T> handler)
        {
            handlers -= handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public override bool IsEmpty => handlers == null || handlers.GetInvocationList().Length == 0;
    }
}
