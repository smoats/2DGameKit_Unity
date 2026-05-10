using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMOD.Studio;
using FMODUnity;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // Bus dictionary
    private Dictionary<string, Bus> buses = new Dictionary<string, Bus>();

    // VCA dictionary
    private Dictionary<string, VCA> vcas = new Dictionary<string, VCA>();

    // Pause variables
    public EventReference pauseSnapshotReference;
    private EventInstance pauseSnapshot;
    private const float MIN_VOLUME_THRESHOLD = 0.0001f; // -80 dB
    private bool pauseState = false;
    private Coroutine pauseCheckCoroutine;

    // Variables for assessing ThreatLevel
    public int numberOfMonstersDetectedBy = 0;
    private bool detectedByMonster = false;
    private bool detectedByMonsterOld = false;

    #region INITIALIZATION
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFMOD();
            InitializeBuses();
            InitializeVCAs();
            pauseSnapshot = RuntimeManager.CreateInstance(pauseSnapshotReference);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    // Start is called before the first frame update
    void Start()
    {

    }

    private void InitializeFMOD()
    {
        // Load banks, initialize runtime settings, etc.
        RuntimeManager.LoadBank("Master");
        bool isBankLoaded = RuntimeManager.HasBankLoaded("Master");
        Debug.Log($"Bank Loaded: {isBankLoaded}");

        RuntimeManager.LoadBank("MusicAmbience");
        isBankLoaded = RuntimeManager.HasBankLoaded("MusicAmbience");
        Debug.Log($"Bank Loaded: {isBankLoaded}");

        RuntimeManager.LoadBank("SFX");
        isBankLoaded = RuntimeManager.HasBankLoaded("SFX");
        Debug.Log($"Bank Loaded: {isBankLoaded}");

        RuntimeManager.LoadBank("UI");
        isBankLoaded = RuntimeManager.HasBankLoaded("UI");
        Debug.Log($"Bank Loaded: {isBankLoaded}");

        RuntimeManager.LoadBank("VOICE");
        isBankLoaded = RuntimeManager.HasBankLoaded("VOICE");
        Debug.Log($"Bank Loaded: {isBankLoaded}");
    }
    #endregion


    #region BUS MANAGEMENT
    // Used e.g. for event management in buses - not for volume changes
    private void InitializeBuses()
    {
        // Define your FMOD buses
        AddBus("Master", "bus:/");
        AddBus("Ambience", "bus:/AMBIENCE");
        AddBus("Music", "bus:/MUSIC");
        AddBus("SFX", "bus:/SFX");
        AddBus("Voice", "bus:/VOICE");

        // Validate buses
        ValidateBuses();
    }

    private void AddBus(string key, string busPath)
    {
        Bus bus = FMODUnity.RuntimeManager.GetBus(busPath);
        buses[key] = bus;
    }

    private void ValidateBuses()
    {
        foreach (var bus in buses)
        {
            if (!bus.Value.isValid())
            {
                Debug.LogError($"FMOD Bus '{bus.Key}' is not valid! Check the FMOD paths.");
            }
        }
    }
    #endregion


    #region VCA MANAGEMENT
    // Used for volume changes e.g. in options menu and game state changes
    private void InitializeVCAs()
    {
        // Define your FMOD VCAs
        AddVCA("VCA_Master", "vca:/Master");
        AddVCA("VCA_SFX", "vca:/SFX");
        AddVCA("VCA_Music", "vca:/Music");
        AddVCA("VCA_Pause", "vca:/Pause");

        // Validate VCAs
        ValidateVCAs();
    }

    private void AddVCA(string key, string vcaPath)
    {
        VCA vca = FMODUnity.RuntimeManager.GetVCA(vcaPath);
        vcas[key] = vca;
    }

    private void ValidateVCAs()
    {
        foreach (var vca in vcas)
        {
            if (!vca.Value.isValid())
            {
                Debug.LogError($"FMOD VCA '{vca.Key}' is not valid! Check the FMOD paths.");
            }
        }
    }

    // Public method to set the volume of a VCA
    public void SetVCAVolume(string vcaName, float volume)
    {
        if (vcas.TryGetValue(vcaName, out VCA vca))
        {
            vca.setVolume(volume);
            Debug.Log($"Setting VCA '{vcaName}' volume to '{volume}'");
        }
        else
        {
            Debug.LogWarning($"FMOD VCA '{vcaName}' not found.");
        }
    }

    // Public method to get the volume of a VCA
    public float GetVCAVolume(string vcaName)
    {
        if (vcas.TryGetValue(vcaName, out VCA vca))
        {
            vca.getVolume(out float volume);
            return volume;
        }
        Debug.LogWarning($"FMOD VCA '{vcaName}' not found.");
        return 0f;
    }
    #endregion


    // Update is called once per frame
    void Update()
    {
        Debug.Log(numberOfMonstersDetectedBy);
        if (numberOfMonstersDetectedBy > 0)
        {
            detectedByMonster = true;
        }
        else
        {
            detectedByMonster = false;
        }

        if (detectedByMonster != detectedByMonsterOld)
        {
            if (detectedByMonster)
            {
                Instance.SetGlobalParameter("ThreatLevel", 1f);
                Debug.Log("Threat level: " + 1);
            }
            else
            {
                Instance.SetGlobalParameter("ThreatLevel", 0f);
                Debug.Log("Threat level: " + 0);
            }

            detectedByMonsterOld = detectedByMonster;
        }

        //Debug.Log(numberOfMonstersDetectedBy);
    }


    #region EVENT MANAGEMENT METHODS
    // Play a one-shot sound event
    public void PlaySound(EventReference eventReference, Vector3 position)
    {
        RuntimeManager.PlayOneShot(eventReference, position);
    }

    // Set a global parameter (e.g., master volume)
    public void SetGlobalParameter(string parameterName, float value)
    {
        RuntimeManager.StudioSystem.setParameterByName(parameterName, value);
    }

    public bool IsStopped(EventInstance fmodInstance)
    {
        PLAYBACK_STATE state;
        fmodInstance.getPlaybackState(out state);
        return state == PLAYBACK_STATE.STOPPED;
    }
    #endregion


    #region PAUSE STATE METHODS
    public void SetPauseState (bool pause)
    {
        Debug.Log("PlayerCharacter pause call: " + pause);

        if (pause != pauseState)
        {
            pauseState = pause;
            Debug.Log("Pause state: " + pauseState);

            if (pause)
            {
                pauseSnapshot.start();

                // Stop any existing coroutine before starting a new one
                if (pauseCheckCoroutine != null)
                {
                    StopCoroutine(pauseCheckCoroutine);
                }

                Debug.Log("Game paused, starting coroutine");
                pauseCheckCoroutine = StartCoroutine(CheckVCAandPauseBuses());
            }
            else
            {
                pauseSnapshot.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                PauseBuses(false);
                Debug.Log("Game unpaused, unpausing SFX and VOICE events");

                // Stop the coroutine if it is running
                if (pauseCheckCoroutine != null)
                {
                    StopCoroutine(pauseCheckCoroutine);
                    pauseCheckCoroutine = null;
                    Debug.Log("Stopping coroutine");
                }
            }
        }
    }

    private IEnumerator CheckVCAandPauseBuses()
    {
        float vcaVolume, vcaFinalVolume;

        while (pauseState) // Stops checking when unpaused
        {
            vcas["VCA_Pause"].getVolume(out vcaVolume, out vcaFinalVolume);
            //Debug.Log("Pause State inside coroutine " + pauseState);

            if (vcaFinalVolume <= MIN_VOLUME_THRESHOLD)
            {
                PauseBuses(true);
                pauseCheckCoroutine = null; // Mark coroutine as completed
                yield break; // Stop the coroutine
            }

            //Debug.Log("Finished loop of coroutine");
            yield return new WaitForSecondsRealtime(0.1f);
        }

        //Debug.Log("CheckAndPauseBuses coroutine exited due to unpause.");
        pauseCheckCoroutine = null; // Clean up
    }

    private void PauseBuses(bool paused)
    {
        buses["SFX"].setPaused(paused);
        buses["Voice"].setPaused(paused);
        Debug.Log("SFX and VOICE events pause state: ");
    }

    #endregion
}
