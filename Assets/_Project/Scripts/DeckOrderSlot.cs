using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>편성 슬롯의 좌클릭 드래그를 편성 화면으로 전달한다.</summary>
public class DeckOrderSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private BattleRewardUI owner;
    [SerializeField] private int index;

    public void OnBeginDrag(PointerEventData data) { if (owner != null) owner.BeginOrderDrag(index, data); }
    public void OnDrag(PointerEventData data) { if (owner != null) owner.UpdateOrderDrag(data); }
    public void OnEndDrag(PointerEventData data) { if (owner != null) owner.FinishOrderDrag(data); }
}
