using System;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 서버 UI Toolkit View 베이스. 일반 C# 클래스(MonoBehaviour 아님) — 클라 패턴과 동일.
    /// UIDocument를 소유한 호스트가 UXML 루트를 Initialize로 주입하고 생명주기를 구동한다.
    /// 서버엔 WindowManager(밴드×스택)가 없으므로 Layer/모달 등 화면관리 개념은 두지 않는다.
    /// </summary>
    public abstract class UIView : IDisposable
    {
        public VisualElement Root { get; private set; }

        /// <summary>호스트가 UXML 루트를 주입하며 1회 호출. 파생은 base 호출 후 바인딩.</summary>
        public virtual void Initialize(VisualElement root)
        {
            Root = root;
        }

        /// <summary>표시 직전 호출.</summary>
        public virtual void OnOpen() { }

        /// <summary>제거 직전 호출.</summary>
        public virtual void OnClose() { }

        public virtual void Dispose() { }
    }
}
