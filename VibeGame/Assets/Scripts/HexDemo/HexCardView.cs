using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HexDemo
{
    public sealed class HexCardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private HexBattleController _controller;
        private HexCardInstance _card;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Transform _originalParent;
        private Vector2 _originalAnchoredPosition;
        private Canvas _rootCanvas;
        private Image _image;

        public void Initialize(HexBattleController controller, HexCardInstance card, Canvas rootCanvas)
        {
            _controller = controller;
            _card = card;
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _rootCanvas = rootCanvas;
            _image = GetComponent<Image>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_controller == null)
                return;

            _originalParent = _rectTransform.parent;
            _originalAnchoredPosition = _rectTransform.anchoredPosition;
            if (_rootCanvas != null)
                _rectTransform.SetParent(_rootCanvas.transform, true);
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0f;
            if (_image != null)
                _image.raycastTarget = false;
            _controller.BeginCardDrag(_card);
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_controller == null)
                return;

            _rectTransform.position = eventData.position;
            _controller.UpdateDraggedCard(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_controller == null)
            {
                Destroy(gameObject);
                return;
            }

            _canvasGroup.blocksRaycasts = true;
            bool played = _controller.EndCardDrag(eventData.position);
            if (!played)
            {
                _rectTransform.SetParent(_originalParent, false);
                _rectTransform.anchoredPosition = _originalAnchoredPosition;
                _canvasGroup.alpha = 1f;
                if (_image != null)
                    _image.raycastTarget = true;
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
