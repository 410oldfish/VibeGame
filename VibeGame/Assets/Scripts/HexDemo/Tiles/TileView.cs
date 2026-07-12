using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HexDemo
{
    public sealed class TileView : MonoBehaviour
    {
        private Transform _structureRoot;
        private GameObject _structureObject;
        private Canvas _hpCanvas;
        private Image _hpFill;
        private TextMeshProUGUI _hpText;

        public void Initialize(Transform structureRoot)
        {
            _structureRoot = structureRoot;
            if (_structureRoot == null)
            {
                var go = new GameObject("StructureRoot");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 0.1f;
                _structureRoot = go.transform;
            }
        }

        public void RefreshStructure(TileModel model, GameObject ruinPrefab = null, GameObject barrierPrefab = null)
        {
            if (_structureRoot == null || model == null)
                return;

            if (_structureObject != null)
                DestroyImmediateSafe(_structureObject);

            if (model.structureType == HexTerrainStructureType.None)
                return;

            if (model.structureType == HexTerrainStructureType.Ruin)
            {
                _structureObject = CreateStructure(
                    "RuinVisual",
                    ruinPrefab,
                    PrimitiveType.Cube,
                    new Vector3(0.6f, 0.35f, 0.6f),
                    new Color(0.52f, 0.41f, 0.3f, 1f));
            }
            else if (model.structureType == HexTerrainStructureType.Barrier)
            {
                _structureObject = CreateStructure(
                    "BarrierVisual",
                    barrierPrefab,
                    PrimitiveType.Sphere,
                    new Vector3(0.6f, 0.25f, 0.6f),
                    new Color(0.36f, 0.36f, 0.42f, 1f));
            }
        }

        public void RefreshHpBar(TileModel model)
        {
            if (model == null || !model.HasRuin)
            {
                SetHpBarVisible(false);
                return;
            }

            EnsureHpBar();
            SetHpBarVisible(true);
            float ratio = model.structureMaxHp > 0
                ? Mathf.Clamp01((float)model.structureHp / model.structureMaxHp)
                : 0f;
            if (_hpFill != null)
                _hpFill.fillAmount = ratio;
            if (_hpText != null)
                _hpText.text = $"{model.structureHp}/{model.structureMaxHp}";
        }

        private void EnsureHpBar()
        {
            if (_hpCanvas != null)
                return;

            var canvasGo = new GameObject("RuinHpBar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = Vector3.one * 0.01f;

            _hpCanvas = canvasGo.GetComponent<Canvas>();
            _hpCanvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(110f, 18f);

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.05f, 0.9f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(canvasGo.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.03f, 0.15f);
            fillRect.anchorMax = new Vector2(0.97f, 0.85f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            _hpFill = fillGo.GetComponent<Image>();
            _hpFill.color = new Color(0.86f, 0.55f, 0.2f, 1f);
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _hpFill.fillAmount = 1f;

            var textGo = new GameObject("HpText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(canvasGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            _hpText = textGo.GetComponent<TextMeshProUGUI>();
            HexTMPFontProvider.ApplyTo(_hpText);
            _hpText.fontSize = 14f;
            _hpText.alignment = TextAlignmentOptions.Center;
            _hpText.color = Color.white;
            _hpText.text = "0/0";
        }

        private void SetHpBarVisible(bool visible)
        {
            if (_hpCanvas != null)
                _hpCanvas.gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            if (_hpCanvas == null || !_hpCanvas.gameObject.activeSelf)
                return;

            var cam = Camera.main;
            if (cam != null)
                _hpCanvas.transform.rotation = Quaternion.LookRotation(_hpCanvas.transform.position - cam.transform.position);
        }

        private GameObject CreateStructure(string name, GameObject prefab, PrimitiveType primitiveType, Vector3 localScale, Color fallbackColor)
        {
            GameObject go = prefab != null ? Instantiate(prefab, _structureRoot) : GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            go.transform.SetParent(_structureRoot, false);
            go.transform.localPosition = Vector3.up * 0.15f;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            if (prefab == null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(renderer.material);
                    renderer.material.color = fallbackColor;
                }
            }

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            return go;
        }

        private static void DestroyImmediateSafe(Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }
    }
}
