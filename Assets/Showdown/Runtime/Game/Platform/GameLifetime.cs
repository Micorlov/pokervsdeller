using System;
using UnityEngine;

namespace Showdown.Game
{
    /// <summary>Ties the game layer's disposables to the host object, so a destroyed host leaves no timers or event handlers behind.</summary>
    public sealed class GameLifetime : MonoBehaviour
    {
        private IDisposable[] owned = Array.Empty<IDisposable>();

        public void Own(params IDisposable[] disposables)
        {
            owned = disposables;
        }

        private void OnDestroy()
        {
            foreach (var disposable in owned)
            {
                disposable.Dispose();
            }

            owned = Array.Empty<IDisposable>();
        }
    }
}
