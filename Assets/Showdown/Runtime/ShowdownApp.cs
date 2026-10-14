using Showdown.Game;
using Showdown.Qa;
using Showdown.UI;
using UnityEngine;

namespace Showdown
{
    /// <summary>
    /// The one object in the scene. It stands up the UI host, then the game layer, then the app shell.
    /// </summary>
    public sealed class ShowdownApp : MonoBehaviour
    {
        private AppRoot root;

        private void Awake()
        {
            gameObject.AddComponent<UiHost>();
            if (QaCapture.Requested) gameObject.AddComponent<QaCapture>();
        }

        private void Start()
        {
            GameBootstrap.Create(UiHost.Instance);
            root = new AppRoot(UiHost.Instance.SafeRoot);
            AppServices.Navigator = root;

            string scenario = QaScenarios.Requested;
            if (scenario != null) StartCoroutine(QaScenarios.Run(scenario));
        }
    }
}
