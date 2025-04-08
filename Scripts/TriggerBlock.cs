using UdonSharp;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Persistence;

namespace BUDDYWORKS.WorldExtension
{
    [AddComponentMenu("BUDDYWORKS/Trigger Block")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class TriggerBlock : UdonSharpBehaviour
    {
        [Header("Send a message to a different Udon Behaviour.")]
        [SerializeField] private UdonSharpBehaviour outputMessageBehaviour;
        [SerializeField] private string _outputMessage = "OnTrigger";

        [Header("Toggle a GameObject when triggered.")]
        [SerializeField] private GameObject outputGameObject;
        [SerializeField] private bool _isSynced;

        [Header("Persistence Settings for GameObject Toggles, avoid using with Synced.")]
        [SerializeField] private bool _isPersisted;
        [Tooltip("Set this string to something unique when using Is Persisted. Using the same Identifier might cause other things to trigger, too.")]
        [SerializeField] private string _persistenceIdentifier = "unique.identifier";

        bool toggled; // Local variable for non-synced and persisted toggling.
        [UdonSynced] bool toggledSynced; // Synced variable for synced toggling.

        [Header("Run when this GameObject is getting enabled or disabled. (Only for Local tasks.)")]
        [SerializeField] private bool _onEnableActive;
        [SerializeField] private bool _onDisableActive;

        void Start()
        {
            // Initialize state based on persistence, sync, and GameObject's active state.
            if (_isPersisted)
            {
                LoadPersistentState();
            }
            else if (_isSynced)
            {
                toggledSynced = outputGameObject.activeSelf;
            }
            else
            {
                toggled = outputGameObject.activeSelf;
            }
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (_isPersisted && _persistenceIdentifier != null)
            {
                if (player == Networking.LocalPlayer)
                {
                    LoadPersistentState();
                }
            }
        }

        public override void OnDeserialization()
        {
            if (_isSynced)
            {
                if (!Networking.IsOwner(gameObject))
                {
                    outputGameObject.SetActive(toggledSynced);
                }
            }
        }

        private void OnEnable()
        {
            if (_onEnableActive) { OnTrigger(); }
        }

        private void OnDisable()
        {
            if (_onDisableActive) { OnTrigger(); }
        }

        public void OnTrigger()
        {
            Debug.Log("BW - Event Triggered");
            if (outputMessageBehaviour != null)
            {
                CastEvent();
            }

            if (outputGameObject != null)
            {
                CastToggle();
            }
        }

        private void CastEvent()
        {
            outputMessageBehaviour.SendCustomEvent(_outputMessage);
            Debug.Log("BW - Message <" + _outputMessage + "> casted to <" + outputMessageBehaviour + ">");
        }

        private void CastToggle()
        {
            Debug.Log("BW - GameObject <" + outputGameObject + "> toggled. Synced " + _isSynced + ". Persisted " + _isPersisted + ".");

            if (_isSynced)
            {
                if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
                toggledSynced = !toggledSynced;
                RequestSerialization();
                outputGameObject.SetActive(toggledSynced);
            }
            else
            {
                toggled = !toggled;
                outputGameObject.SetActive(toggled);

                if (_isPersisted)
                {
                    SavePersistentState();
                }
            }
        }

        private void LoadPersistentState()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer; // Get the local player.
            if (localPlayer != null && PlayerData.TryGetBool(localPlayer, _persistenceIdentifier, out bool persistedState))
            {
                toggled = persistedState;
                outputGameObject.SetActive(toggled);
            }
            else
            {
                // If no persistent data exists, initialize with the GameObject's current state.
                toggled = outputGameObject.activeSelf;
                SavePersistentState(); // Save the initial state.
            }
        }

        private void SavePersistentState()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer; // Get the local player.
            if (localPlayer != null)
            {
                PlayerData.SetBool(_persistenceIdentifier, toggled);
            }
        }
    }
}