using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// Shows a hand built results panel when the shift timer runs out, with the number of orders
    /// delivered and late. Wire the buttons' OnClick to Restart and GoToMainMenu.
    /// </summary>
    [DisallowMultipleComponent]
    public class ResultsPanel : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("The results panel. Hidden until the shift ends.")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text deliveredText;
        [SerializeField] private TMP_Text lateText;
        [Tooltip("Optional. Leave empty if the panel has no money line.")]
        [SerializeField] private TMP_Text moneyText;

        [Header("Scenes")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Wiring (found automatically if left empty)")]
        [SerializeField] private ShiftTimer shiftTimer;
        [SerializeField] private OrderManager orderManager;

        private int _delivered;
        private int _late;
        private float _money;
        private bool _shiftOver;

        private void Awake()
        {
            if (shiftTimer == null) shiftTimer = FindAnyObjectByType<ShiftTimer>();
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();

            if (panel != null) panel.SetActive(false);

            if (shiftTimer == null || orderManager == null)
            {
                Debug.LogError("[ResultsPanel] Needs a ShiftTimer and an OrderManager in the scene.", this);
                enabled = false;
                return;
            }

            orderManager.OrderResolved += HandleOrderResolved;
            shiftTimer.ShiftEnded += HandleShiftEnded;
        }

        private void OnDestroy()
        {
            if (orderManager != null) orderManager.OrderResolved -= HandleOrderResolved;
            if (shiftTimer != null) shiftTimer.ShiftEnded -= HandleShiftEnded;
        }

        private void HandleOrderResolved(Order order, float value)
        {
            if (_shiftOver) return; // nothing after the timer counts

            if (order.State == OrderState.Delivered)
            {
                _delivered++;
                _money += value;
            }
            else
            {
                _late++;
            }
        }

        private void HandleShiftEnded()
        {
            _shiftOver = true;

            if (deliveredText != null) deliveredText.text = $"Delivered: {_delivered}";
            if (lateText != null) lateText.text = $"Late: {_late}";
            if (moneyText != null) moneyText.text = $"Earned: ${_money:0}";

            if (panel != null) panel.SetActive(true);

            Time.timeScale = 0f; // freeze the game behind the panel
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f;

            if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                Debug.LogError($"[ResultsPanel] Cannot load \"{mainMenuSceneName}\". Check the name and " +
                               "that it is in File > Build Profiles > Scene List.", this);
                return;
            }

            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}