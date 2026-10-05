using ProjectHymn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectHymn.UI
{
    /// <summary>
    /// A boss's health across the bottom of the screen (VS-3): name, the bar with the phase thresholds marked, and the
    /// phase. Shows once the boss notices the player; hides when it falls.
    /// </summary>
    public class BossHud : MonoBehaviour
    {
        BoneCantor _boss;
        GameObject _root;
        RectTransform _fill;
        Text _name;
        float _shown;

        public static void Track(BoneCantor boss)
        {
            var h = new GameObject("Boss HUD").AddComponent<BossHud>();
            h._boss = boss;
            h.Build(boss.DisplayName);
        }

        void Build(string title)
        {
            var c = UiKit.Canvas("Boss Canvas", 120, transform);
            _root = c.gameObject;
            var frame = UiKit.Rect("Frame", c.transform, new Vector2(0.2f, 0f), new Vector2(0.8f, 0f), new Vector2(0f, 40f), new Vector2(0f, 74f));
            frame.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.04f, 0.09f, 0.9f);
            var back = UiKit.Rect("Back", frame, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            back.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.08f, 0.12f);
            _fill = UiKit.Rect("Fill", back, Vector2.zero, Vector2.one);
            _fill.gameObject.AddComponent<Image>().color = new Color(0.86f, 0.25f, 0.3f);
            foreach (var k in new[] { 1f / 3f, 2f / 3f })
            {
                var mark = UiKit.Rect("Phase Mark", back, new Vector2(k, 0f), new Vector2(k, 1f), new Vector2(-1.5f, 0f), new Vector2(1.5f, 0f));
                mark.gameObject.AddComponent<Image>().color = new Color(1f, 0.85f, 0.45f, 0.9f);
            }
            var nameRt = UiKit.Rect("Name", c.transform, new Vector2(0.2f, 0f), new Vector2(0.8f, 0f), new Vector2(0f, 78f), new Vector2(0f, 118f));
            _name = nameRt.gameObject.AddComponent<Text>();
            _name.font = UiKit.Font; _name.fontSize = 30; _name.alignment = TextAnchor.LowerLeft; _name.color = UiKit.Gold;
            _name.text = title.ToUpperInvariant();
            _root.SetActive(false);
        }

        void Update()
        {
            if (_boss == null || _boss.IsDead || !_boss.gameObject.activeInHierarchy)
            {
                if (_root != null && _root.activeSelf) { _shown += Time.deltaTime; if (_shown > 1.5f) _root.SetActive(false); }
                if (_boss != null && _boss.IsDead && _fill != null) _fill.anchorMax = new Vector2(0f, 1f);
                return;
            }
            if (!_boss.Aggro && _boss.Hp >= _boss.MaxHp) { if (_root.activeSelf) _root.SetActive(false); return; }
            if (!_root.activeSelf) _root.SetActive(true);
            _shown = 0f;
            float k = Mathf.Clamp01(_boss.Hp / Mathf.Max(1f, _boss.MaxHp));
            _fill.anchorMax = new Vector2(k, 1f);
            _name.text = $"{_boss.DisplayName.ToUpperInvariant()}   <size=22><color=#b8b2c8>verse {_boss.BossPhase + 1} of 3</color></size>";
        }
    }
}
