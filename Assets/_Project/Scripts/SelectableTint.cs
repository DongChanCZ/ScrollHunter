using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼의 hover·눌림·선택 상태를 두 번째 그래픽(카드 테두리)에도 작은 밝기 변화로 반영한다.
/// 버튼 자체의 Color Tint(배경)와 함께 써서 테두리·배경이 같이 조금만 밝아지게 한다. 표시 전용.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class SelectableTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Graphic target;
    [SerializeField] private Color normalColor = new Color(0.70f, 0.55f, 0.33f, 0.95f);
    [SerializeField] private Color highlightedColor = new Color(0.86f, 0.70f, 0.44f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.60f, 0.47f, 0.28f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.82f, 0.66f, 0.41f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.70f, 0.55f, 0.33f, 0.95f);

    private Selectable selectable;
    private bool hovered;
    private bool pressed;
    private bool selected;

    private void Awake() => selectable = GetComponent<Selectable>();

    private void OnEnable()
    {
        hovered = pressed = false;
        Apply();
    }

    private void OnDisable()
    {
        hovered = pressed = selected = false;
        Apply();
    }

    // 상호작용 가능 여부는 다른 코드가 바꾸므로 매 프레임 맞춘다.
    private void LateUpdate() => Apply();

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; Apply(); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; Apply(); }
    public void OnPointerDown(PointerEventData eventData) { pressed = true; Apply(); }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; Apply(); }
    public void OnSelect(BaseEventData eventData) { selected = true; Apply(); }
    public void OnDeselect(BaseEventData eventData) { selected = false; Apply(); }

    private void Apply()
    {
        if (target == null) return;
        bool interactable = selectable != null && selectable.IsInteractable();
        target.color = !interactable ? disabledColor : pressed ? pressedColor
            : hovered ? highlightedColor : selected ? selectedColor : normalColor;
    }
}
