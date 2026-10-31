using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// A button in the main menu's Settings panel that switches how the game shows the way:
    /// Markers (beacons and arrows), GPS route, or both. Each click moves to the next option and
    /// the button's text shows the current one, for example "Guidance: Markers". The choice is
    /// saved, and the same setting can be changed in game with G or D-pad up.
    ///
    /// SETUP
    /// Add a Button to the Settings panel, put this component on it, and drag the button's text
    /// into Label (a TextMeshPro text). The button's click is wired up automatically.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class GuidanceSettingButton : MonoBehaviour
    {
        [Tooltip("The button's text. Found in the button's children if left empty.")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private string prefix = "Guidance: ";

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            _button.onClick.AddListener(GameSettings.CycleGuidance);
        }

        private void OnEnable()
        {
            GameSettings.GuidanceChanged += Refresh;
            Refresh(GameSettings.Guidance);
        }

        private void OnDisable()
        {
            GameSettings.GuidanceChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(GameSettings.CycleGuidance);
        }

        private void Refresh(GuidanceMode mode)
        {
            if (label != null) label.text = prefix + GameSettings.Describe(mode);
        }
    }
}
