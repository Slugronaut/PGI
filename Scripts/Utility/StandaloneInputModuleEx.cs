using UnityEngine;
using UnityEngine.EventSystems;

namespace PowerGridInventory
{
    /// <summary>
    /// Unity hides too many important features in their default implementation so we
    /// need to expose some of them using this overriding class.
    /// </summary>
    public class StandaloneInputModuleEx : StandaloneInputModule
    {
        PointerEventData Data;
        public PointerEventData LastPointerEvent(int id)
        {
            return Data;// this.GetLastPointerEventData(id);
        }

        protected override void ProcessMove(PointerEventData pointerEvent)
        {
            Data = pointerEvent;
            base.ProcessMove(pointerEvent);
        }


        static StandaloneInputModuleEx _Instance;
        /// <summary>
        /// Gets an instance of a StandaloneInputModuleEx. If one does not exist
        /// in the scene it will be created automatically.
        /// </summary>
        public static StandaloneInputModuleEx Instance
        {
            get
            {
                #if UNITY_EDITOR
                if (!Application.isPlaying) return null;
                #endif

                if(_Instance == null)
                {
                    var es = FindObjectOfType<EventSystem>();
                    if(es != null)
                    {
                        _Instance = es.GetComponent<StandaloneInputModuleEx>();
                        if(_Instance == null)
                        {
                            var std = es.GetComponent<StandaloneInputModule>();
                            if (std != null)
                            {
                                string hor = std.horizontalAxis;
                                string vert = std.verticalAxis;
                                string submit = std.submitButton;
                                string cancel = std.cancelButton;
                                float ias = std.inputActionsPerSecond;
                                float delay = std.repeatDelay;
                                bool force = std.forceModuleActive;
                                Destroy(std);

                                _Instance = es.gameObject.AddComponent<StandaloneInputModuleEx>();
                                _Instance.horizontalAxis = hor;
                                _Instance.verticalAxis = vert;
                                _Instance.submitButton = submit;
                                _Instance.cancelButton = std.cancelButton;
                                _Instance.inputActionsPerSecond = ias;
                                _Instance.repeatDelay = delay;
                                _Instance.forceModuleActive = force;

                            }
                            else _Instance = es.gameObject.AddComponent<StandaloneInputModuleEx>();
                        }
                    }
                    else
                    {
                        GameObject go = new GameObject("EventSystem");
                        es = go.AddComponent<EventSystem>();
                        _Instance = go.AddComponent<StandaloneInputModuleEx>();
                    }
                }

                return _Instance;
            }
        }
    }
}
