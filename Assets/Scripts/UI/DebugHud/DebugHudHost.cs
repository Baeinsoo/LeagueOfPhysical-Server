using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// UIDocument를 소유하는 얇은 호스트. UI Toolkit 패널 렌더에 MonoBehaviour 하나(UIDocument)는
    /// 필수다 — 일반 C# View를 생성·Initialize하고 생명주기를 구동한다. 서버엔 WindowManager가 없어
    /// 밴드/스택 없이 이 호스트가 단일 View를 직접 띄운다. UXML/USS는 UIDocument.sourceAsset이 소유.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DebugHudHost : MonoBehaviour
    {
        [SerializeField] private UIDocument document;

        private DebugHudView _view;

        private void Start()
        {
            if (document == null) document = GetComponent<UIDocument>();

            _view = new DebugHudView(new DebugHudViewModel());
            _view.Initialize(document.rootVisualElement);
            _view.OnOpen();
        }

        private void OnDestroy()
        {
            _view?.OnClose();
            _view?.Dispose();
            _view = null;
        }
    }
}
