using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// Loads a separate scene for each difficulty. Wire each button's OnClick to one method:
    ///   Easy button   -> LoadEasy
    ///   Medium button -> LoadMedium
    ///   Hard button   -> LoadHard
    /// Each scene name must match the scene file's name exactly, and every scene must be in
    /// File > Build Profiles > Scene List, or Unity cannot load it.
    /// </summary>
    [DisallowMultipleComponent]
    public class DifficultySceneLoader : MonoBehaviour
    {
        [Header("Scene names (exact file names, no .unity)")]
        [SerializeField] private string easySceneName = "Easy";
        [SerializeField] private string mediumSceneName = "Medium";
        [SerializeField] private string hardSceneName = "Hard";

        public void LoadEasy() => Load(easySceneName);

        public void LoadMedium() => Load(mediumSceneName);

        public void LoadHard() => Load(hardSceneName);

        private void Load(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[DifficultySceneLoader] Cannot load scene \"{sceneName}\". Check the " +
                               "name on this component and that the scene is in File > Build Profiles > " +
                               "Scene List.", this);
                return;
            }

            Time.timeScale = 1f;
            Debug.Log($"[DifficultySceneLoader] Loading {sceneName}.");
            SceneManager.LoadScene(sceneName);
        }
    }
}
